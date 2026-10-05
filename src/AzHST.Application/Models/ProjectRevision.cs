namespace AzHST.Application.Models;

public static class ProjectRevisionIds
{
    public const string Current = "000";
}

public enum ProjectGenerationStatus
{
    Ready,
}

public sealed record ProjectRevision(
    string ProjectId,
    string RevisionId,
    string HtmlFilePath,
    string? PresentationFilePath,
    ProjectGenerationStatus GenerationStatus,
    DateTimeOffset UpdatedUtc);
