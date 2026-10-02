using AzHST.Application.Models;

namespace AzHST.Application.Abstractions;

public interface ICopilotSampleQueryGenerator
{
    Task<IReadOnlyList<SampleQuery>> GenerateSampleQueriesAsync(
        IReadOnlyList<SampleQueryCategory> categories,
        int queriesPerCategory,
        string model,
        CancellationToken cancellationToken = default);
}
