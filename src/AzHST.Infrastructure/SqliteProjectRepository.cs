using System.Globalization;
using AzHST.Application.Abstractions;
using AzHST.Application.Models;
using Microsoft.Data.Sqlite;

namespace AzHST.Infrastructure;

public sealed class SqliteProjectRepository : IProjectRepository
{
    private const int ApplicationId = 0x415A4850;
    private const int SchemaVersion = 1;

    private readonly string _databaseFile;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private volatile bool _initialized;

    public SqliteProjectRepository(ApplicationPaths paths)
    {
        _databaseFile = paths.ProjectDatabaseFile;
    }

    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            var parentDirectory = Path.GetDirectoryName(_databaseFile)
                ?? throw new InvalidOperationException(
                    "The project database path has no parent directory.");
            Directory.CreateDirectory(parentDirectory);

            if (File.Exists(_databaseFile))
            {
                try
                {
                    await ValidateDatabaseAsync(cancellationToken);
                }
                catch (Exception exception)
                    when (exception is not OperationCanceledException)
                {
                    throw new InvalidDataException(
                        $"The project database '{_databaseFile}' is invalid or uses an unsupported schema. It was not reset because it contains user history.",
                        exception);
                }
            }
            else
            {
                await CreateDatabaseAsync(cancellationToken);
            }

            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task<IReadOnlyList<Project>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(
            SqliteOpenMode.ReadOnly,
            cancellationToken);
        await using var command = CreateSelectCommand(connection);
        command.CommandText += " ORDER BY projects.updated_utc DESC;";

        var projects = new List<Project>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            projects.Add(ReadProject(reader));
        }

        return projects;
    }

    public async Task<Project?> GetAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(
            SqliteOpenMode.ReadOnly,
            cancellationToken);
        await using var command = CreateSelectCommand(connection);
        command.CommandText += " WHERE projects.id = $projectId LIMIT 1;";
        command.Parameters.AddWithValue("$projectId", projectId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadProject(reader)
            : null;
    }

    public async Task SaveAsync(
        Project project,
        CancellationToken cancellationToken = default)
    {
        ValidateProject(project);
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(
            SqliteOpenMode.ReadWrite,
            cancellationToken);
        await using var transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(
                cancellationToken);

        await using (var projectCommand = connection.CreateCommand())
        {
            projectCommand.Transaction = transaction;
            projectCommand.CommandText =
                """
                INSERT INTO projects
                (
                    id,
                    title,
                    original_query,
                    copilot_session_id,
                    created_utc,
                    updated_utc,
                    selected_model_id,
                    html_theme_id,
                    presentation_theme_id
                )
                VALUES
                (
                    $id,
                    $title,
                    $originalQuery,
                    $copilotSessionId,
                    $createdUtc,
                    $updatedUtc,
                    $selectedModelId,
                    $htmlThemeId,
                    $presentationThemeId
                )
                ON CONFLICT (id) DO UPDATE SET
                    title = excluded.title,
                    original_query = excluded.original_query,
                    copilot_session_id = excluded.copilot_session_id,
                    created_utc = excluded.created_utc,
                    updated_utc = excluded.updated_utc,
                    selected_model_id = excluded.selected_model_id,
                    html_theme_id = excluded.html_theme_id,
                    presentation_theme_id = excluded.presentation_theme_id;
                """;
            projectCommand.Parameters.AddWithValue("$id", project.Id);
            projectCommand.Parameters.AddWithValue("$title", project.Title);
            projectCommand.Parameters.AddWithValue(
                "$originalQuery",
                project.OriginalQuery);
            projectCommand.Parameters.AddWithValue(
                "$copilotSessionId",
                project.CopilotSessionId);
            projectCommand.Parameters.AddWithValue(
                "$createdUtc",
                FormatTimestamp(project.CreatedUtc));
            projectCommand.Parameters.AddWithValue(
                "$updatedUtc",
                FormatTimestamp(project.UpdatedUtc));
            projectCommand.Parameters.AddWithValue(
                "$selectedModelId",
                project.SelectedModelId);
            projectCommand.Parameters.AddWithValue(
                "$htmlThemeId",
                project.HtmlThemeId);
            projectCommand.Parameters.AddWithValue(
                "$presentationThemeId",
                project.PresentationThemeId);
            await projectCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var revisionCommand = connection.CreateCommand())
        {
            revisionCommand.Transaction = transaction;
            revisionCommand.CommandText =
                """
                INSERT INTO project_revisions
                (
                    project_id,
                    revision_id,
                    html_file_path,
                    presentation_file_path,
                    generation_status,
                    updated_utc
                )
                VALUES
                (
                    $projectId,
                    $revisionId,
                    $htmlFilePath,
                    $presentationFilePath,
                    $generationStatus,
                    $updatedUtc
                )
                ON CONFLICT (project_id, revision_id) DO UPDATE SET
                    html_file_path = excluded.html_file_path,
                    presentation_file_path = excluded.presentation_file_path,
                    generation_status = excluded.generation_status,
                    updated_utc = excluded.updated_utc;
                """;
            revisionCommand.Parameters.AddWithValue(
                "$projectId",
                project.Revision.ProjectId);
            revisionCommand.Parameters.AddWithValue(
                "$revisionId",
                project.Revision.RevisionId);
            revisionCommand.Parameters.AddWithValue(
                "$htmlFilePath",
                project.Revision.HtmlFilePath);
            revisionCommand.Parameters.AddWithValue(
                "$presentationFilePath",
                (object?)project.Revision.PresentationFilePath ?? DBNull.Value);
            revisionCommand.Parameters.AddWithValue(
                "$generationStatus",
                project.Revision.GenerationStatus.ToString());
            revisionCommand.Parameters.AddWithValue(
                "$updatedUtc",
                FormatTimestamp(project.Revision.UpdatedUtc));
            await revisionCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(
            SqliteOpenMode.ReadWrite,
            cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM projects WHERE id = $projectId;";
        command.Parameters.AddWithValue("$projectId", projectId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task CreateDatabaseAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(
            SqliteOpenMode.ReadWriteCreate,
            cancellationToken);
        await ExecuteNonQueryAsync(
            connection,
            "PRAGMA journal_mode = WAL;",
            cancellationToken);
        await ExecuteNonQueryAsync(
            connection,
            $"""
            PRAGMA application_id = {ApplicationId};
            PRAGMA user_version = {SchemaVersion};

            CREATE TABLE projects
            (
                id TEXT PRIMARY KEY,
                title TEXT NOT NULL CHECK (length(trim(title)) > 0),
                original_query TEXT NOT NULL
                    CHECK (length(trim(original_query)) > 0),
                copilot_session_id TEXT NOT NULL UNIQUE
                    CHECK (length(trim(copilot_session_id)) > 0),
                created_utc TEXT NOT NULL,
                updated_utc TEXT NOT NULL,
                selected_model_id TEXT NOT NULL,
                html_theme_id TEXT NOT NULL,
                presentation_theme_id TEXT NOT NULL
            ) STRICT;

            CREATE TABLE project_revisions
            (
                project_id TEXT NOT NULL
                    REFERENCES projects (id) ON DELETE CASCADE,
                revision_id TEXT NOT NULL
                    CHECK (length(revision_id) = 3),
                html_file_path TEXT NOT NULL
                    CHECK (length(trim(html_file_path)) > 0),
                presentation_file_path TEXT,
                generation_status TEXT NOT NULL,
                updated_utc TEXT NOT NULL,
                PRIMARY KEY (project_id, revision_id)
            ) STRICT;

            CREATE INDEX ix_projects_updated_utc
                ON projects (updated_utc DESC);
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
            await using var quickCheckReader =
                await quickCheck.ExecuteReaderAsync(cancellationToken);
            var resultCount = 0;
            while (await quickCheckReader.ReadAsync(cancellationToken))
            {
                resultCount++;
                if (!string.Equals(
                        quickCheckReader.GetString(0),
                        "ok",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "The project database failed its SQLite integrity check.");
                }
            }

            if (resultCount != 1)
            {
                throw new InvalidDataException(
                    "The project database returned an unexpected integrity result.");
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
                "The project database has an unsupported schema.");
        }

        await ValidateQueryAsync(
            connection,
            """
            SELECT
                id,
                title,
                original_query,
                copilot_session_id,
                created_utc,
                updated_utc,
                selected_model_id,
                html_theme_id,
                presentation_theme_id
            FROM projects
            LIMIT 0;
            """,
            cancellationToken);
        await ValidateQueryAsync(
            connection,
            """
            SELECT
                project_id,
                revision_id,
                html_file_path,
                presentation_file_path,
                generation_status,
                updated_utc
            FROM project_revisions
            LIMIT 0;
            """,
            cancellationToken);

        await using var foreignKeyCheck = connection.CreateCommand();
        foreignKeyCheck.CommandText = "PRAGMA foreign_key_check;";
        await using var reader =
            await foreignKeyCheck.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidDataException(
                "The project database contains invalid revision references.");
        }

        await using var currentRevisionCheck = connection.CreateCommand();
        currentRevisionCheck.CommandText =
            """
            SELECT COUNT(*)
            FROM projects
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM project_revisions
                WHERE project_revisions.project_id = projects.id
                    AND project_revisions.revision_id = $revisionId
            );
            """;
        currentRevisionCheck.Parameters.AddWithValue(
            "$revisionId",
            ProjectRevisionIds.Current);
        var missingRevisionCount = Convert.ToInt32(
            await currentRevisionCheck.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);
        if (missingRevisionCount > 0)
        {
            throw new InvalidDataException(
                "The project database contains projects without revision 000.");
        }
    }

    private static SqliteCommand CreateSelectCommand(
        SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                projects.id,
                projects.title,
                projects.original_query,
                projects.copilot_session_id,
                projects.created_utc,
                projects.updated_utc,
                projects.selected_model_id,
                projects.html_theme_id,
                projects.presentation_theme_id,
                revisions.revision_id,
                revisions.html_file_path,
                revisions.presentation_file_path,
                revisions.generation_status,
                revisions.updated_utc
            FROM projects
            INNER JOIN project_revisions AS revisions
                ON revisions.project_id = projects.id
                AND revisions.revision_id = $revisionId
            """;
        command.Parameters.AddWithValue(
            "$revisionId",
            ProjectRevisionIds.Current);
        return command;
    }

    private static Project ReadProject(SqliteDataReader reader)
    {
        var projectId = reader.GetString(0);
        var revisionStatus = reader.GetString(12);
        if (!Enum.TryParse<ProjectGenerationStatus>(
                revisionStatus,
                ignoreCase: false,
                out var generationStatus))
        {
            throw new InvalidDataException(
                $"Project '{projectId}' has an unsupported generation status.");
        }

        var revision = new ProjectRevision(
            projectId,
            reader.GetString(9),
            reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetString(11),
            generationStatus,
            ParseTimestamp(reader.GetString(13)));
        return new Project(
            projectId,
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            ParseTimestamp(reader.GetString(4)),
            ParseTimestamp(reader.GetString(5)),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetString(8),
            revision);
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
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> ExecutePragmaIntAsync(
        SqliteConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static async Task ValidateQueryAsync(
        SqliteConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);
    }

    private static void ValidateProject(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (string.IsNullOrWhiteSpace(project.Id)
            || string.IsNullOrWhiteSpace(project.Title)
            || string.IsNullOrWhiteSpace(project.OriginalQuery)
            || string.IsNullOrWhiteSpace(project.CopilotSessionId)
            || string.IsNullOrWhiteSpace(project.Revision.HtmlFilePath))
        {
            throw new ArgumentException(
                "The project is missing required data.",
                nameof(project));
        }

        if (!string.Equals(
                project.Id,
                project.Revision.ProjectId,
                StringComparison.Ordinal)
            || !string.Equals(
                project.Revision.RevisionId,
                ProjectRevisionIds.Current,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The project revision does not match the current project.",
                nameof(project));
        }
    }

    private static string FormatTimestamp(DateTimeOffset timestamp)
    {
        return timestamp.ToUniversalTime().ToString(
            "O",
            CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset ParseTimestamp(string timestamp)
    {
        if (!DateTimeOffset.TryParseExact(
                timestamp,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            throw new InvalidDataException(
                "The project database contains an invalid timestamp.");
        }

        return parsed;
    }
}
