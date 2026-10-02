using AzHST.Application.Abstractions;
using AzHST.Application.Exceptions;
using AzHST.Application.Models;

namespace AzHST.Application.Services;

public sealed class SampleQueryUseCase
{
    public const int QueriesPerCategory = 10;
    public const int HomeCategoryCount = 4;
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromDays(7);

    private const int MaximumQueryLength = 400;

    private readonly ISampleQueryRepository _repository;
    private readonly ICopilotSampleQueryGenerator _generator;
    private readonly TimeProvider _timeProvider;

    public SampleQueryUseCase(
        ISampleQueryRepository repository,
        ICopilotSampleQueryGenerator generator,
        TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _generator = generator;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<SampleQueryStoreInitialization> InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        var initialization = await _repository.InitializeAsync(cancellationToken);
        var generatedAt = await _repository.GetGeneratedAtAsync(cancellationToken);
        var counts = await _repository.GetQueryCountsByCategoryAsync(cancellationToken);

        if (generatedAt is null || !HasCompleteQuerySet(counts))
        {
            var seedQueries = NormalizeQuerySet(SampleQueryCatalog.SeedQueries);
            await _repository.ReplaceAllAsync(
                SampleQueryCatalog.Categories,
                seedQueries,
                _timeProvider.GetUtcNow(),
                cancellationToken);
        }

        return initialization;
    }

    public async Task<bool> RefreshIfStaleAsync(
        CancellationToken cancellationToken = default)
    {
        var generatedAt = await _repository.GetGeneratedAtAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow();

        if (generatedAt is not null
            && now - generatedAt.Value < RefreshInterval)
        {
            return false;
        }

        var generatedQueries = await _generator.GenerateSampleQueriesAsync(
            SampleQueryCatalog.Categories,
            QueriesPerCategory,
            CopilotModelSelection.Automatic,
            cancellationToken);
        var normalizedQueries = NormalizeQuerySet(generatedQueries);

        await _repository.ReplaceAllAsync(
            SampleQueryCatalog.Categories,
            normalizedQueries,
            now,
            cancellationToken);

        return true;
    }

    public async Task<IReadOnlyList<SampleQuery>> GetHomeSuggestionsAsync(
        CancellationToken cancellationToken = default)
    {
        var suggestions = await _repository.GetRandomAsync(
            HomeCategoryCount,
            cancellationToken);

        if (suggestions.Count != HomeCategoryCount
            || suggestions.Select(item => item.CategoryId)
                .Distinct(StringComparer.Ordinal)
                .Count() != HomeCategoryCount)
        {
            throw new InvalidDataException(
                "The local sample query database did not return four distinct categories.");
        }

        return suggestions;
    }

    private static bool HasCompleteQuerySet(
        IReadOnlyDictionary<string, int> counts)
    {
        return counts.Count == SampleQueryCatalog.Categories.Count
            && SampleQueryCatalog.Categories.All(
                category => counts.TryGetValue(category.Id, out var count)
                    && count == QueriesPerCategory);
    }

    private static IReadOnlyList<SampleQuery> NormalizeQuerySet(
        IReadOnlyList<SampleQuery> queries)
    {
        ArgumentNullException.ThrowIfNull(queries);

        var expectedCount =
            SampleQueryCatalog.Categories.Count * QueriesPerCategory;
        if (queries.Count != expectedCount)
        {
            throw new SampleQueryGenerationException(
                $"Expected {expectedCount} sample queries but received {queries.Count}.");
        }

        var normalized = new List<SampleQuery>(expectedCount);
        var globalQueries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var category in SampleQueryCatalog.Categories)
        {
            var categoryQueries = queries
                .Where(query => string.Equals(
                    query.CategoryId,
                    category.Id,
                    StringComparison.Ordinal))
                .ToArray();

            if (categoryQueries.Length != QueriesPerCategory)
            {
                throw new SampleQueryGenerationException(
                    $"Category '{category.Id}' must contain exactly {QueriesPerCategory} sample queries.");
            }

            var categoryQueryTexts = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var sample in categoryQueries)
            {
                var queryText = NormalizeWhitespace(sample.Query);
                if (queryText.Length == 0)
                {
                    throw new SampleQueryGenerationException(
                        $"Category '{category.Id}' contains an empty sample query.");
                }

                if (queryText.Length > MaximumQueryLength)
                {
                    throw new SampleQueryGenerationException(
                        $"A sample query in category '{category.Id}' exceeds {MaximumQueryLength} characters.");
                }

                if (!categoryQueryTexts.Add(queryText)
                    || !globalQueries.Add(queryText))
                {
                    throw new SampleQueryGenerationException(
                        $"The generated sample query '{queryText}' is duplicated.");
                }

                normalized.Add(new SampleQuery(
                    category.Id,
                    category.DisplayName,
                    queryText));
            }
        }

        return normalized;
    }

    private static string NormalizeWhitespace(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(
            " ",
            value.Split(
                [' ', '\t', '\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries));
    }
}
