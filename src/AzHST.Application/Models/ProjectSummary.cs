namespace AzHST.Application.Models;

public sealed record ProjectSummary(
    string Id,
    string Title,
    string OriginalQuery,
    DateTimeOffset UpdatedUtc);

public sealed record ProjectSummaryPage(
    IReadOnlyList<ProjectSummary> Items,
    bool HasMore,
    int NextOffset);
