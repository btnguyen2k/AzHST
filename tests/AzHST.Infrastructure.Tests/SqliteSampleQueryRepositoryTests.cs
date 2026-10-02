using AzHST.Application.Models;

namespace AzHST.Infrastructure.Tests;

public sealed class SqliteSampleQueryRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset GeneratedAt =
        new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "AzHST.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task InitializeAsync_CreatesDatabaseWithSupportedSchema()
    {
        var paths = CreatePaths();
        var repository = new SqliteSampleQueryRepository(paths);

        var result = await repository.InitializeAsync();
        var secondResult = await repository.InitializeAsync();

        Assert.True(result.WasCreated);
        Assert.False(result.WasReset);
        Assert.False(secondResult.WasCreated);
        Assert.False(secondResult.WasReset);
        Assert.True(File.Exists(paths.SampleQueryDatabaseFile));
        Assert.Null(await repository.GetGeneratedAtAsync());
        Assert.Empty(await repository.GetQueryCountsByCategoryAsync());
    }

    [Fact]
    public async Task InitializeAsync_ResetsInvalidDatabase()
    {
        var paths = CreatePaths();
        Directory.CreateDirectory(
            Path.GetDirectoryName(paths.SampleQueryDatabaseFile)!);
        await File.WriteAllTextAsync(
            paths.SampleQueryDatabaseFile,
            "This is not a SQLite database.");
        var repository = new SqliteSampleQueryRepository(paths);

        var result = await repository.InitializeAsync();

        Assert.False(result.WasCreated);
        Assert.True(result.WasReset);
        Assert.Null(await repository.GetGeneratedAtAsync());
        Assert.Empty(await repository.GetQueryCountsByCategoryAsync());
    }

    [Fact]
    public async Task ReplaceAllAsync_PersistsTimestampAndReturnsOneQueryPerRandomCategory()
    {
        var paths = CreatePaths();
        var repository = new SqliteSampleQueryRepository(paths);
        await repository.InitializeAsync();

        await repository.ReplaceAllAsync(
            SampleQueryCatalog.Categories,
            SampleQueryCatalog.SeedQueries,
            GeneratedAt);

        var counts = await repository.GetQueryCountsByCategoryAsync();
        var samples = await repository.GetRandomAsync(4);

        Assert.Equal(GeneratedAt, await repository.GetGeneratedAtAsync());
        Assert.Equal(SampleQueryCatalog.Categories.Count, counts.Count);
        Assert.All(
            counts,
            pair => Assert.Equal(10, pair.Value));
        Assert.Equal(4, samples.Count);
        Assert.Equal(
            4,
            samples.Select(sample => sample.CategoryId).Distinct().Count());
        Assert.All(
            samples,
            sample => Assert.Contains(
                SampleQueryCatalog.SeedQueries,
                expected => expected.CategoryId == sample.CategoryId
                    && expected.Query == sample.Query));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private ApplicationPaths CreatePaths()
    {
        var localData = Path.Combine(_testDirectory, "local");
        return new ApplicationPaths(
            localData,
            Path.Combine(localData, "settings.json"),
            Path.Combine(_testDirectory, "generated"),
            Path.Combine(localData, "copilot"),
            Path.Combine(_testDirectory, "data", "azhst.db"));
    }
}
