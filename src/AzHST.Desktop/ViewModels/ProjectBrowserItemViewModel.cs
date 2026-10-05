using AzHST.Application.Models;

namespace AzHST.Desktop.ViewModels;

public sealed class ProjectBrowserItemViewModel
{
    private const int MaximumQueryPreviewLength = 180;

    public ProjectBrowserItemViewModel(
        ProjectSummary project,
        bool isActive)
    {
        Id = project.Id;
        Title = project.Title;
        QueryPreview = CreateQueryPreview(project.OriginalQuery);
        UpdatedText = $"Updated {project.UpdatedUtc.ToLocalTime():g}";
        IsActive = isActive;
    }

    public string Id { get; }

    public string Title { get; }

    public string QueryPreview { get; }

    public string UpdatedText { get; }

    public bool IsActive { get; }

    private static string CreateQueryPreview(string query)
    {
        var normalized = string.Join(
            " ",
            query.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries
                    | StringSplitOptions.TrimEntries));
        return normalized.Length <= MaximumQueryPreviewLength
            ? normalized
            : $"{normalized[..(MaximumQueryPreviewLength - 1)].TrimEnd()}…";
    }
}
