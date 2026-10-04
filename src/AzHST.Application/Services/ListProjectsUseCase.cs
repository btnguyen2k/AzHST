using AzHST.Application.Abstractions;
using AzHST.Application.Models;

namespace AzHST.Application.Services;

public sealed class ListProjectsUseCase
{
    private readonly IProjectRepository _repository;

    public ListProjectsUseCase(IProjectRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<Project>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        return _repository.ListAsync(cancellationToken);
    }
}
