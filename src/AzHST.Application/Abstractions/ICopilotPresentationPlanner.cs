using AzHST.Application.Models;

namespace AzHST.Application.Abstractions;

public interface ICopilotPresentationPlanner
{
    Task<PresentationPlan> CreatePresentationPlanAsync(
        string query,
        string model,
        string visualizationId,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
