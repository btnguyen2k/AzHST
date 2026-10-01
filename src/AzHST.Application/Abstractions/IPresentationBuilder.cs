using AzHST.Application.Models;

namespace AzHST.Application.Abstractions;

public interface IPresentationBuilder
{
    Task<PresentationArtifact> BuildAsync(
        PresentationPlan plan,
        VisualizationArtifact visualization,
        CancellationToken cancellationToken = default);
}
