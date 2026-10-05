using AzHST.Application.Abstractions;
using AzHST.Application.Exceptions;
using AzHST.Application.Models;

namespace AzHST.Application.Services;

public sealed class RenameProjectUseCase
{
    public const int MaximumTitleLength = 100;

    private readonly IProjectRepository _repository;
    private readonly TimeProvider _timeProvider;

    public RenameProjectUseCase(
        IProjectRepository repository,
        TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<Project> ExecuteAsync(
        string projectId,
        string title,
        CancellationToken cancellationToken = default)
    {
        var normalizedTitle = title?.Trim();
        if (string.IsNullOrEmpty(normalizedTitle))
        {
            throw new ArgumentException(
                "Enter a project title.",
                nameof(title));
        }

        if (normalizedTitle.Length > MaximumTitleLength)
        {
            throw new ArgumentException(
                $"The project title must be {MaximumTitleLength} characters or fewer.",
                nameof(title));
        }

        var project = await _repository.GetAsync(projectId, cancellationToken)
            ?? throw new ProjectNotFoundException(projectId);
        var updatedProject = project with
        {
            Title = normalizedTitle,
            UpdatedUtc = _timeProvider.GetUtcNow(),
        };

        await _repository.SaveAsync(updatedProject, cancellationToken);
        return updatedProject;
    }
}
