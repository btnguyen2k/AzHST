namespace AzHST.Application.Models;

public sealed class VisualizationQueryAssessment
{
    public bool IsValid { get; set; }

    public string Message { get; set; } = string.Empty;

    public string SuggestedSlug { get; set; } = string.Empty;
}
