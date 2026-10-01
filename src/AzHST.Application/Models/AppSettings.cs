namespace AzHST.Application.Models;

public sealed record AppSettings
{
    public string Model { get; init; } = CopilotModelSelection.Automatic;

    public string OutputDirectory { get; init; } = string.Empty;

    public bool OpenResultsInExternalBrowser { get; init; }

    public OutputThemeSettings Themes { get; init; } = new();
}
