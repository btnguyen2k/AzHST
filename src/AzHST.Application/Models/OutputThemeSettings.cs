namespace AzHST.Application.Models;

public sealed record OutputThemeSettings
{
    public const string DefaultHtmlThemeId = "azure-night";
    public const string DefaultPresentationThemeId = "professional-light";

    public string HtmlThemeId { get; init; } = DefaultHtmlThemeId;

    public string PresentationThemeId { get; init; } = DefaultPresentationThemeId;
}

public sealed record OutputThemeOption(
    string Id,
    string DisplayName);
