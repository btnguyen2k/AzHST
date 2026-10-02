namespace AzHST.Application.Models;

public sealed record SampleQueryCategory(
    string Id,
    string DisplayName,
    string Description);

public sealed record SampleQuery(
    string CategoryId,
    string CategoryName,
    string Query);
