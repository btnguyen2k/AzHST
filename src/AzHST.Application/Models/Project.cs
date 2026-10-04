namespace AzHST.Application.Models;

public sealed record Project(
    string Id,
    string Title,
    string OriginalQuery,
    string CopilotSessionId,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc,
    string SelectedModelId,
    string HtmlThemeId,
    string PresentationThemeId,
    ProjectRevision Revision);
