using AzHST.Application.Abstractions;
using AzHST.Application.Exceptions;
using AzHST.Application.Models;

namespace AzHST.Application.Services;

public sealed class OpenProjectUseCase
{
    private readonly IProjectRepository _repository;
    private readonly IProjectArtifactStore _artifactStore;

    public OpenProjectUseCase(
        IProjectRepository repository,
        IProjectArtifactStore artifactStore)
    {
        _repository = repository;
        _artifactStore = artifactStore;
    }

    public async Task<ProjectWorkspace> ExecuteAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        var project = await _repository.GetAsync(projectId, cancellationToken)
            ?? throw new ProjectNotFoundException(projectId);
        var artifact = await _artifactStore.LoadAsync(
            project,
            cancellationToken);

        return new ProjectWorkspace(project, artifact);
    }
}
