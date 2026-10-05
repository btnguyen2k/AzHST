using AzHST.Application.Models;

namespace AzHST.Application.Abstractions;

public interface IProjectRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Project>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<ProjectSummaryPage> ListSummariesAsync(
        string searchText,
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Project?> GetAsync(
        string projectId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        Project project,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string projectId,
        CancellationToken cancellationToken = default);
}
