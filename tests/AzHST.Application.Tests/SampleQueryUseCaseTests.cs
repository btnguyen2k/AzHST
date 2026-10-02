using AzHST.Application.Abstractions;
using AzHST.Application.Exceptions;
using AzHST.Application.Models;
using AzHST.Application.Services;

namespace AzHST.Application.Tests;

public sealed class SampleQueryUseCaseTests
{
    private static readonly DateTimeOffset FixedTime =
        new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task InitializeAsync_SeedsTenQueriesForEveryCategory()
    {
        var repository = new StubSampleQueryRepository();
        var generator = new StubSampleQueryGenerator([]);
        var useCase = CreateUseCase(repository, generator);

        var result = await useCase.InitializeAsync();

        Assert.True(result.WasCreated);
        Assert.False(result.WasReset);
        Assert.Equal(FixedTime, repository.GeneratedAt);
        Assert.Equal(
            SampleQueryCatalog.Categories.Count * SampleQueryUseCase.QueriesPerCategory,
            repository.Queries.Count);
        Assert.All(
            SampleQueryCatalog.Categories,
            category => Assert.Equal(
                SampleQueryUseCase.QueriesPerCategory,
                repository.Queries.Count(query => query.CategoryId == category.Id)));
        Assert.Equal(
            repository.Queries.Count,
            repository.Queries
                .Select(query => query.Query)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());
        Assert.Equal(0, generator.CallCount);
    }

    [Fact]
    public async Task RefreshIfStaleAsync_DoesNotGenerateBeforeOneWeek()
    {
        var repository = CreatePopulatedRepository(
            FixedTime - TimeSpan.FromDays(6));
        var generator = new StubSampleQueryGenerator(
            CreateGeneratedQueries("New"));
        var useCase = CreateUseCase(repository, generator);

        var refreshed = await useCase.RefreshIfStaleAsync();

        Assert.False(refreshed);
        Assert.Equal(0, generator.CallCount);
        Assert.Equal(
            SampleQueryCatalog.SeedQueries.Select(query => query.Query),
            repository.Queries.Select(query => query.Query));
    }

    [Fact]
    public async Task RefreshIfStaleAsync_ReplacesAllQueriesAtOneWeek()
    {
        var repository = CreatePopulatedRepository(
            FixedTime - SampleQueryUseCase.RefreshInterval);
        var generatedQueries = CreateGeneratedQueries("Refreshed");
        var generator = new StubSampleQueryGenerator(generatedQueries);
        var useCase = CreateUseCase(repository, generator);

        var refreshed = await useCase.RefreshIfStaleAsync();

        Assert.True(refreshed);
        Assert.Equal(1, generator.CallCount);
        Assert.Equal(CopilotModelSelection.Automatic, generator.Model);
        Assert.Equal(SampleQueryUseCase.QueriesPerCategory, generator.QueryCount);
        Assert.Equal(FixedTime, repository.GeneratedAt);
        Assert.Equal(
            generatedQueries.Select(query => query.Query),
            repository.Queries.Select(query => query.Query));
    }

    [Fact]
    public async Task RefreshIfStaleAsync_DoesNotReplaceIncompleteGeneration()
    {
        var repository = CreatePopulatedRepository(
            FixedTime - SampleQueryUseCase.RefreshInterval);
        var originalQueries = repository.Queries.ToArray();
        var generatedQueries = CreateGeneratedQueries("Incomplete")
            .Take(SampleQueryCatalog.SeedQueries.Count - 1)
            .ToArray();
        var generator = new StubSampleQueryGenerator(generatedQueries);
        var useCase = CreateUseCase(repository, generator);

        await Assert.ThrowsAsync<SampleQueryGenerationException>(
            () => useCase.RefreshIfStaleAsync());

        Assert.Equal(
            originalQueries.Select(query => query.Query),
            repository.Queries.Select(query => query.Query));
        Assert.Equal(
            FixedTime - SampleQueryUseCase.RefreshInterval,
            repository.GeneratedAt);
    }

    [Fact]
    public async Task GetHomeSuggestionsAsync_ReturnsFourDistinctCategories()
    {
        var repository = CreatePopulatedRepository(FixedTime);
        var useCase = CreateUseCase(
            repository,
            new StubSampleQueryGenerator([]));

        var suggestions = await useCase.GetHomeSuggestionsAsync();

        Assert.Equal(SampleQueryUseCase.HomeCategoryCount, suggestions.Count);
        Assert.Equal(
            SampleQueryUseCase.HomeCategoryCount,
            suggestions.Select(query => query.CategoryId).Distinct().Count());
    }

    private static SampleQueryUseCase CreateUseCase(
        StubSampleQueryRepository repository,
        StubSampleQueryGenerator generator)
    {
        return new SampleQueryUseCase(
            repository,
            generator,
            new FixedTimeProvider(FixedTime));
    }

    private static StubSampleQueryRepository CreatePopulatedRepository(
        DateTimeOffset generatedAt)
    {
        return new StubSampleQueryRepository
        {
            Categories = SampleQueryCatalog.Categories.ToArray(),
            Queries = SampleQueryCatalog.SeedQueries.ToArray(),
            GeneratedAt = generatedAt,
        };
    }

    private static IReadOnlyList<SampleQuery> CreateGeneratedQueries(
        string prefix)
    {
        return SampleQueryCatalog.SeedQueries
            .Select((query, index) => query with
            {
                Query = $"{prefix} {index + 1}: {query.Query}",
            })
            .ToArray();
    }

    private sealed class StubSampleQueryGenerator(
        IReadOnlyList<SampleQuery> result) : ICopilotSampleQueryGenerator
    {
        public int CallCount { get; private set; }

        public int QueryCount { get; private set; }

        public string? Model { get; private set; }

        public Task<IReadOnlyList<SampleQuery>> GenerateSampleQueriesAsync(
            IReadOnlyList<SampleQueryCategory> categories,
            int queriesPerCategory,
            string model,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            QueryCount = queriesPerCategory;
            Model = model;
            return Task.FromResult(result);
        }
    }

    private sealed class StubSampleQueryRepository : ISampleQueryRepository
    {
        public SampleQueryStoreInitialization Initialization { get; set; } =
            new(WasCreated: true, WasReset: false);

        public IReadOnlyList<SampleQueryCategory> Categories { get; set; } = [];

        public IReadOnlyList<SampleQuery> Queries { get; set; } = [];

        public DateTimeOffset? GeneratedAt { get; set; }

        public Task<SampleQueryStoreInitialization> InitializeAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Initialization);
        }

        public Task<DateTimeOffset?> GetGeneratedAtAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GeneratedAt);
        }

        public Task<IReadOnlyDictionary<string, int>> GetQueryCountsByCategoryAsync(
            CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<string, int> counts = Queries
                .GroupBy(query => query.CategoryId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Count(),
                    StringComparer.Ordinal);
            return Task.FromResult(counts);
        }

        public Task ReplaceAllAsync(
            IReadOnlyList<SampleQueryCategory> categories,
            IReadOnlyList<SampleQuery> queries,
            DateTimeOffset generatedAt,
            CancellationToken cancellationToken = default)
        {
            Categories = categories.ToArray();
            Queries = queries.ToArray();
            GeneratedAt = generatedAt;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SampleQuery>> GetRandomAsync(
            int categoryCount,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<SampleQuery> samples = Queries
                .GroupBy(query => query.CategoryId, StringComparer.Ordinal)
                .Take(categoryCount)
                .Select(group => group.First())
                .ToArray();
            return Task.FromResult(samples);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
