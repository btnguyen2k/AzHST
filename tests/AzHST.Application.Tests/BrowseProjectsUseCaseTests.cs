using AzHST.Application.Abstractions;
using AzHST.Application.Models;
using AzHST.Application.Services;

namespace AzHST.Application.Tests;

public sealed class BrowseProjectsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_NormalizesAndForwardsPagingRequest()
    {
        var expected = new ProjectSummaryPage(
            [
                new ProjectSummary(
                    "project-id",
                    "Application Gateway",
                    "Explain Application Gateway",
                    DateTimeOffset.UtcNow),
            ],
            HasMore: true,
            NextOffset: 12);
        var repository = new StubProjectRepository(expected);
        var useCase = new BrowseProjectsUseCase(repository);

        var result = await useCase.ExecuteAsync(
            "  gateway  ",
            offset: 7,
            pageSize: 5);

        Assert.Same(expected, result);
        Assert.Equal("gateway", repository.SearchText);
        Assert.Equal(7, repository.Offset);
        Assert.Equal(5, repository.PageSize);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsInvalidPagingAndSearchValues()
    {
        var useCase = new BrowseProjectsUseCase(
            new StubProjectRepository(
                new ProjectSummaryPage([], false, 0)));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => useCase.ExecuteAsync(offset: -1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => useCase.ExecuteAsync(pageSize: 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => useCase.ExecuteAsync(
                pageSize: BrowseProjectsUseCase.MaximumPageSize + 1));
        await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.ExecuteAsync(
                new string(
                    'x',
                    BrowseProjectsUseCase.MaximumSearchLength + 1)));
    }

    private sealed class StubProjectRepository(
        ProjectSummaryPage result) : IProjectRepository
    {
        public string? SearchText { get; private set; }

        public int Offset { get; private set; }

        public int PageSize { get; private set; }

        public Task<ProjectSummaryPage> ListSummariesAsync(
            string searchText,
            int offset,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            SearchText = searchText;
            Offset = offset;
            PageSize = pageSize;
            return Task.FromResult(result);
        }

        public Task InitializeAsync(
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<Project>> ListAsync(
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Project?> GetAsync(
            string projectId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SaveAsync(
            Project project,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAsync(
            string projectId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
