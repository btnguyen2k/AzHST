using System.Globalization;
using AzHST.Application.Abstractions;
using AzHST.Application.Models;
using Microsoft.Data.Sqlite;

namespace AzHST.Infrastructure;

public sealed class SqliteSampleQueryRepository : ISampleQueryRepository
{
    private const int ApplicationId = 0x415A4853;
    private const int SchemaVersion = 1;
    private const string GeneratedAtKey = "sample_queries_generated_utc";

    private readonly string _databaseFile;

    public SqliteSampleQueryRepository(ApplicationPaths paths)
    {
        _databaseFile = paths.SampleQueryDatabaseFile;
    }

    public async Task<SampleQueryStoreInitialization> InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_databaseFile)
            ?? throw new InvalidOperationException(
                "The sample query database path does not have a parent directory.");

        Directory.CreateDirectory(directory);

        if (!File.Exists(_databaseFile))
        {
            await CreateDatabaseAsync(cancellationToken);
            return new SampleQueryStoreInitialization(
                WasCreated: true,
                WasReset: false);
        }

        try
        {
            await ValidateDatabaseAsync(cancellationToken);
            return new SampleQueryStoreInitialization(
                WasCreated: false,
                WasReset: false);
        }
        catch (InvalidDataException)
        {
            await ResetDatabaseAsync(cancellationToken);
            return new SampleQueryStoreInitialization(
                WasCreated: false,
                WasReset: true);
        }
        catch (SqliteException exception) when (CanReset(exception))
        {
            await ResetDatabaseAsync(cancellationToken);
            return new SampleQueryStoreInitialization(
                WasCreated: false,
                WasReset: true);
        }
    }

    public async Task<DateTimeOffset?> GetGeneratedAtAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(
            SqliteOpenMode.ReadOnly,
            cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT value FROM metadata WHERE key = $key LIMIT 1;";
        command.Parameters.AddWithValue("$key", GeneratedAtKey);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is null or DBNull)
        {
            return null;
        }

        if (!DateTimeOffset.TryParseExact(
                Convert.ToString(value, CultureInfo.InvariantCulture),
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var generatedAt))
        {
            throw new InvalidDataException(
                "The sample query generation timestamp is invalid.");
        }

        return generatedAt;
    }

    public async Task<IReadOnlyDictionary<string, int>> GetQueryCountsByCategoryAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(
            SqliteOpenMode.ReadOnly,
            cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT category_id, COUNT(*)
            FROM sample_queries
            GROUP BY category_id;
            """;

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            counts.Add(reader.GetString(0), reader.GetInt32(1));
        }

        return counts;
    }

    public async Task ReplaceAllAsync(
        IReadOnlyList<SampleQueryCategory> categories,
        IReadOnlyList<SampleQuery> queries,
        DateTimeOffset generatedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(queries);

        if (categories.Count == 0)
        {
            throw new ArgumentException(
                "At least one sample query category is required.",
                nameof(categories));
        }

        var categoryIds = categories
            .Select(category => category.Id)
            .ToHashSet(StringComparer.Ordinal);
        if (categoryIds.Count != categories.Count
            || queries.Any(query => !categoryIds.Contains(query.CategoryId)))
        {
            throw new ArgumentException(
                "Sample query categories must be unique and every query must reference one of them.",
                nameof(categories));
        }

        await using var connection = await OpenConnectionAsync(
            SqliteOpenMode.ReadWrite,
            cancellationToken);
        await using var transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            transaction,
            """
            DELETE FROM sample_queries;
            DELETE FROM categories;
            DELETE FROM metadata WHERE key = $key;
            """,
            cancellationToken,
            ("$key", GeneratedAtKey));

        await using (var categoryCommand = connection.CreateCommand())
        {
            categoryCommand.Transaction = transaction;
            categoryCommand.CommandText = """
                INSERT INTO categories (id, display_name, description, sort_order)
                VALUES ($id, $displayName, $description, $sortOrder);
                """;
            var id = categoryCommand.Parameters.Add("$id", SqliteType.Text);
            var displayName =
                categoryCommand.Parameters.Add("$displayName", SqliteType.Text);
            var description =
                categoryCommand.Parameters.Add("$description", SqliteType.Text);
            var sortOrder =
                categoryCommand.Parameters.Add("$sortOrder", SqliteType.Integer);

            for (var index = 0; index < categories.Count; index++)
            {
                var category = categories[index];
                id.Value = category.Id;
                displayName.Value = category.DisplayName;
                description.Value = category.Description;
                sortOrder.Value = index;
                await categoryCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        var generatedAtText = generatedAt.ToUniversalTime().ToString(
            "O",
            CultureInfo.InvariantCulture);

        await using (var queryCommand = connection.CreateCommand())
        {
            queryCommand.Transaction = transaction;
            queryCommand.CommandText = """
                INSERT INTO sample_queries (category_id, query_text, created_utc)
                VALUES ($categoryId, $queryText, $createdUtc);
                """;
            var categoryId =
                queryCommand.Parameters.Add("$categoryId", SqliteType.Text);
            var queryText =
                queryCommand.Parameters.Add("$queryText", SqliteType.Text);
            var createdUtc =
                queryCommand.Parameters.Add("$createdUtc", SqliteType.Text);

            foreach (var query in queries)
            {
                categoryId.Value = query.CategoryId;
                queryText.Value = query.Query;
                createdUtc.Value = generatedAtText;
                await queryCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await ExecuteNonQueryAsync(
            connection,
            transaction,
            """
            INSERT INTO metadata (key, value)
            VALUES ($key, $value);
            """,
            cancellationToken,
            ("$key", GeneratedAtKey),
            ("$value", generatedAtText));

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SampleQuery>> GetRandomAsync(
        int categoryCount,
        CancellationToken cancellationToken = default)
    {
        if (categoryCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(categoryCount),
                "The category count must be greater than zero.");
        }

        await using var connection = await OpenConnectionAsync(
            SqliteOpenMode.ReadOnly,
            cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            WITH selected_categories AS
            (
                SELECT id, display_name
                FROM categories
                ORDER BY random()
                LIMIT $categoryCount
            ),
            ranked_queries AS
            (
                SELECT
                    queries.category_id,
                    categories.display_name,
                    queries.query_text,
                    ROW_NUMBER() OVER
                    (
                        PARTITION BY queries.category_id
                        ORDER BY random()
                    ) AS query_rank
                FROM selected_categories AS categories
                INNER JOIN sample_queries AS queries
                    ON queries.category_id = categories.id
            )
            SELECT category_id, display_name, query_text
            FROM ranked_queries
            WHERE query_rank = 1
            ORDER BY random();
            """;
        command.Parameters.AddWithValue("$categoryCount", categoryCount);

        var samples = new List<SampleQuery>(categoryCount);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            samples.Add(new SampleQuery(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2)));
        }

        return samples;
    }

    private async Task CreateDatabaseAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(
            SqliteOpenMode.ReadWriteCreate,
            cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            transaction: null,
            "PRAGMA journal_mode = WAL;",
            cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            transaction: null,
            $"""
            PRAGMA application_id = {ApplicationId};
            PRAGMA user_version = {SchemaVersion};

            CREATE TABLE categories
            (
                id TEXT PRIMARY KEY,
                display_name TEXT NOT NULL,
                description TEXT NOT NULL,
                sort_order INTEGER NOT NULL UNIQUE CHECK (sort_order >= 0)
            ) STRICT;

            CREATE TABLE sample_queries
            (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                category_id TEXT NOT NULL
                    REFERENCES categories (id) ON DELETE CASCADE,
                query_text TEXT NOT NULL
                    CHECK (length(trim(query_text)) > 0),
                created_utc TEXT NOT NULL,
                UNIQUE (category_id, query_text)
            ) STRICT;

            CREATE INDEX ix_sample_queries_category_id
                ON sample_queries (category_id);

            CREATE TABLE metadata
            (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            ) STRICT;
            """,
            cancellationToken);
    }

    private async Task ValidateDatabaseAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(
            SqliteOpenMode.ReadOnly,
            cancellationToken);

        await using (var quickCheck = connection.CreateCommand())
        {
            quickCheck.CommandText = "PRAGMA quick_check;";
            await using var reader =
                await quickCheck.ExecuteReaderAsync(cancellationToken);
            var resultCount = 0;

            while (await reader.ReadAsync(cancellationToken))
            {
                resultCount++;
                if (!string.Equals(
                        reader.GetString(0),
                        "ok",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "The sample query database failed its SQLite integrity check.");
                }
            }

            if (resultCount != 1)
            {
                throw new InvalidDataException(
                    "The sample query database returned an unexpected integrity result.");
            }
        }

        if (await ExecutePragmaIntAsync(
                connection,
                "PRAGMA application_id;",
                cancellationToken) != ApplicationId
            || await ExecutePragmaIntAsync(
                connection,
                "PRAGMA user_version;",
                cancellationToken) != SchemaVersion)
        {
            throw new InvalidDataException(
                "The sample query database has an unsupported schema.");
        }

        await ValidateQueryAsync(
            connection,
            "SELECT id, display_name, description, sort_order FROM categories LIMIT 0;",
            cancellationToken);
        await ValidateQueryAsync(
            connection,
            "SELECT id, category_id, query_text, created_utc FROM sample_queries LIMIT 0;",
            cancellationToken);
        await ValidateQueryAsync(
            connection,
            "SELECT key, value FROM metadata LIMIT 0;",
            cancellationToken);

        await using (var foreignKeyCheck = connection.CreateCommand())
        {
            foreignKeyCheck.CommandText = "PRAGMA foreign_key_check;";
            await using var reader =
                await foreignKeyCheck.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidDataException(
                    "The sample query database contains invalid category references.");
            }
        }

        await using var timestampCommand = connection.CreateCommand();
        timestampCommand.CommandText =
            "SELECT value FROM metadata WHERE key = $key LIMIT 1;";
        timestampCommand.Parameters.AddWithValue("$key", GeneratedAtKey);
        var timestamp = await timestampCommand.ExecuteScalarAsync(cancellationToken);

        if (timestamp is not null
            && timestamp is not DBNull
            && !DateTimeOffset.TryParseExact(
                Convert.ToString(timestamp, CultureInfo.InvariantCulture),
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out _))
        {
            throw new InvalidDataException(
                "The sample query database contains an invalid generation timestamp.");
        }
    }

    private async Task ResetDatabaseAsync(CancellationToken cancellationToken)
    {
        DeleteIfExists(_databaseFile);
        DeleteIfExists($"{_databaseFile}-wal");
        DeleteIfExists($"{_databaseFile}-shm");
        await CreateDatabaseAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(
        SqliteOpenMode mode,
        CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = _databaseFile,
                Mode = mode,
                Pooling = false,
            }.ToString());

        try
        {
            await connection.OpenAsync(cancellationToken);
            await ExecuteNonQueryAsync(
                connection,
                transaction: null,
                """
                PRAGMA foreign_keys = ON;
                PRAGMA busy_timeout = 5000;
                """,
                cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string commandText,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;

        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> ExecutePragmaIntAsync(
        SqliteConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static async Task ValidateQueryAsync(
        SqliteConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    }

    private static bool CanReset(SqliteException exception)
    {
        return exception.SqliteErrorCode is 1 or 11 or 26;
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
