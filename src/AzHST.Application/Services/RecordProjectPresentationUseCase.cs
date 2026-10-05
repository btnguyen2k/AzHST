using AzHST.Application.Abstractions;
using AzHST.Application.Exceptions;
using AzHST.Application.Models;

namespace AzHST.Application.Services;

public sealed class RecordProjectPresentationUseCase
{
    private readonly IProjectRepository _repository;
    private readonly TimeProvider _timeProvider;

    public RecordProjectPresentationUseCase(
        IProjectRepository repository,
        TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<Project> ExecuteAsync(
        string projectId,
        PresentationArtifact presentation,
        string presentationThemeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentException.ThrowIfNullOrWhiteSpace(presentationThemeId);

        var project = await _repository.GetAsync(projectId, cancellationToken)
            ?? throw new ProjectNotFoundException(projectId);
        if (!string.Equals(
                project.Id,
                presentation.VisualizationId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The presentation does not belong to this project.",
                nameof(presentation));
        }

        var now = _timeProvider.GetUtcNow();
        var updatedProject = project with
        {
            UpdatedUtc = now,
            PresentationThemeId = presentationThemeId,
            Revision = project.Revision with
            {
                PresentationFilePath = presentation.FilePath,
                UpdatedUtc = now,
            },
        };
        await _repository.SaveAsync(updatedProject, cancellationToken);
        return updatedProject;
    }
}
