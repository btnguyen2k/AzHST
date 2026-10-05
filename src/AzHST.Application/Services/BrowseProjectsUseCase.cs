using AzHST.Application.Abstractions;
using AzHST.Application.Models;

namespace AzHST.Application.Services;

public sealed class BrowseProjectsUseCase
{
    public const int DefaultPageSize = 50;
    public const int MaximumPageSize = 100;
    public const int MaximumSearchLength = 200;

    private readonly IProjectRepository _repository;

    public BrowseProjectsUseCase(IProjectRepository repository)
    {
        _repository = repository;
    }

    public Task<ProjectSummaryPage> ExecuteAsync(
        string? searchText = null,
        int offset = 0,
        int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            pageSize,
            MaximumPageSize);

        var normalizedSearch = searchText?.Trim() ?? string.Empty;
        if (normalizedSearch.Length > MaximumSearchLength)
        {
            throw new ArgumentException(
                $"Project search text cannot exceed {MaximumSearchLength} characters.",
                nameof(searchText));
        }

        return _repository.ListSummariesAsync(
            normalizedSearch,
            offset,
            pageSize,
            cancellationToken);
    }
}
