using AzHST.Application.Models;

namespace AzHST.Application.Abstractions;

public interface IProjectArtifactStore
{
    Task<VisualizationArtifact> SaveAsync(
        string projectId,
        string revisionId,
        string html,
        string outputDirectory,
        bool overwrite,
        CancellationToken cancellationToken = default);

    Task<VisualizationArtifact> LoadAsync(
        Project project,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Project project,
        CancellationToken cancellationToken = default);
}
