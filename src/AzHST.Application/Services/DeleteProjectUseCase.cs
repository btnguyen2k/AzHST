using AzHST.Application.Abstractions;
using AzHST.Application.Exceptions;

namespace AzHST.Application.Services;

public sealed class DeleteProjectUseCase
{
    private readonly IProjectRepository _repository;
    private readonly IProjectArtifactStore _artifactStore;
    private readonly ICopilotProjectConversation _conversation;

    public DeleteProjectUseCase(
        IProjectRepository repository,
        IProjectArtifactStore artifactStore,
        ICopilotProjectConversation conversation)
    {
        _repository = repository;
        _artifactStore = artifactStore;
        _conversation = conversation;
    }

    public async Task ExecuteAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        var project = await _repository.GetAsync(projectId, cancellationToken)
            ?? throw new ProjectNotFoundException(projectId);

        await _conversation.DeleteAsync(
            project.CopilotSessionId,
            cancellationToken);
        await _artifactStore.DeleteAsync(project, cancellationToken);
        await _repository.DeleteAsync(project.Id, cancellationToken);
    }
}
