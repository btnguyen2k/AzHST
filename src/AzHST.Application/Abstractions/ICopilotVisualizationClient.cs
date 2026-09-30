using AzHST.Application.Models;

namespace AzHST.Application.Abstractions;

public interface ICopilotVisualizationClient
{
    Task<string> GenerateHtmlAsync(
        string query,
        string model,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
