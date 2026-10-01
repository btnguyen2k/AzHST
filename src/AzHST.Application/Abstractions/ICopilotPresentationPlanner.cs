using AzHST.Application.Models;

namespace AzHST.Application.Abstractions;

public interface ICopilotPresentationPlanner
{
    Task<PresentationPlan> CreatePresentationPlanAsync(
        string query,
        string model,
        VisualizationArtifact visualization,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
