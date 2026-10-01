namespace AzHST.Application.Models;

public sealed record AppSettings
{
    public const string DefaultModel = "auto";

    public string Model { get; init; } = DefaultModel;

    public string OutputDirectory { get; init; } = string.Empty;

    public bool OpenResultsInExternalBrowser { get; init; }

    public OutputThemeSettings Themes { get; init; } = new();
}
