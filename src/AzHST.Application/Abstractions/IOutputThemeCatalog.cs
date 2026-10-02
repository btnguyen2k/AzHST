using AzHST.Application.Models;

namespace AzHST.Application.Abstractions;

public interface IOutputThemeCatalog
{
    IReadOnlyList<OutputThemeOption> HtmlThemes { get; }

    IReadOnlyList<OutputThemeOption> PresentationThemes { get; }

    HtmlThemeDefinition GetHtmlTheme(string id);

    PresentationThemeDefinition GetPresentationTheme(string id);
}
