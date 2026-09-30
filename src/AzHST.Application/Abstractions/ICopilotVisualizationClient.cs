using AzHST.Application.Models;

namespace AzHST.Application.Abstractions;

public interface ICopilotVisualizationClient
{
    Task<VisualizationQueryAssessment> AssessQueryAsync(
        string query,
        string model,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<string> GenerateHtmlAsync(
        string query,
        string model,
        string visualizationId,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
