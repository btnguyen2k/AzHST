using AzHST.Application.Models;

namespace AzHST.Application.Abstractions;

public interface ISampleQueryRepository
{
    Task<SampleQueryStoreInitialization> InitializeAsync(
        CancellationToken cancellationToken = default);

    Task<DateTimeOffset?> GetGeneratedAtAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, int>> GetQueryCountsByCategoryAsync(
        CancellationToken cancellationToken = default);

    Task ReplaceAllAsync(
        IReadOnlyList<SampleQueryCategory> categories,
        IReadOnlyList<SampleQuery> queries,
        DateTimeOffset generatedAt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SampleQuery>> GetRandomAsync(
        int categoryCount,
        CancellationToken cancellationToken = default);
}
