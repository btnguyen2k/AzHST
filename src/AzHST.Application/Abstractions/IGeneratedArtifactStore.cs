using AzHST.Application.Models;

namespace AzHST.Application.Abstractions;

public interface IGeneratedArtifactStore
{
    Task<VisualizationArtifact> SaveAsync(
        string visualizationId,
        string html,
        string outputDirectory,
        CancellationToken cancellationToken = default);
}
