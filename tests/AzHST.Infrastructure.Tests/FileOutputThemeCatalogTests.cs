using System.Text.Json;
using System.Text.Json.Nodes;
using AzHST.Application.Exceptions;
using AzHST.Application.Models;

namespace AzHST.Infrastructure.Tests;

public sealed class FileOutputThemeCatalogTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "AzHST.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Constructor_LoadsBundledThemesWithoutThemeVersion()
    {
        var themeDirectory = CopyBundledThemes();

        var catalog = new FileOutputThemeCatalog(themeDirectory);

        Assert.Equal(
            OutputThemeSettings.DefaultHtmlThemeId,
            Assert.Single(catalog.HtmlThemes).Id);
        Assert.Equal(
            OutputThemeSettings.DefaultPresentationThemeId,
            Assert.Single(catalog.PresentationThemes).Id);
        Assert.Equal(
            "Azure Night",
            catalog.GetHtmlTheme(
                OutputThemeSettings.DefaultHtmlThemeId).DisplayName);
        Assert.Equal(
            "Professional Light",
            catalog.GetPresentationTheme(
                OutputThemeSettings.DefaultPresentationThemeId).DisplayName);

        Assert.False(ReadTheme(
            Path.Combine(themeDirectory, "html", "azure-night.json"))
            .ContainsKey("version"));
        Assert.False(ReadTheme(
            Path.Combine(
                themeDirectory,
                "presentation",
                "professional-light.json"))
            .ContainsKey("version"));
    }

    [Fact]
    public void Constructor_RejectsThemeVersionAsUnknownConfiguration()
    {
        var themeDirectory = CopyBundledThemes();
        var file = Path.Combine(
            themeDirectory,
            "html",
            "azure-night.json");
        var theme = ReadTheme(file);
        theme["version"] = "1.0";
        WriteTheme(file, theme);

        var exception = Assert.Throws<OutputThemeConfigurationException>(
            () => new FileOutputThemeCatalog(themeDirectory));

        Assert.Contains(
            "version",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_RejectsMalformedColor()
    {
        var themeDirectory = CopyBundledThemes();
        var file = Path.Combine(
            themeDirectory,
            "html",
            "azure-night.json");
        var theme = ReadTheme(file);
        theme["palette"]!["page"] = "navy";
        WriteTheme(file, theme);

        var exception = Assert.Throws<OutputThemeConfigurationException>(
            () => new FileOutputThemeCatalog(themeDirectory));

        Assert.Contains("Use #RRGGBB", exception.Message);
    }

    [Fact]
    public void Constructor_RejectsInsufficientTextContrast()
    {
        var themeDirectory = CopyBundledThemes();
        var file = Path.Combine(
            themeDirectory,
            "html",
            "azure-night.json");
        var theme = ReadTheme(file);
        theme["palette"]!["text"] = theme["palette"]!["page"]!.GetValue<string>();
        WriteTheme(file, theme);

        var exception = Assert.Throws<OutputThemeConfigurationException>(
            () => new FileOutputThemeCatalog(themeDirectory));

        Assert.Contains("insufficient contrast", exception.Message);
    }

    [Fact]
    public void GetHtmlTheme_RejectsUnavailableSelection()
    {
        var catalog = new FileOutputThemeCatalog(CopyBundledThemes());

        var exception = Assert.Throws<OutputThemeConfigurationException>(
            () => catalog.GetHtmlTheme("missing-theme"));

        Assert.Contains("unavailable", exception.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private string CopyBundledThemes()
    {
        var source = FindBundledThemeDirectory();
        var destination = Path.Combine(_testDirectory, "themes");
        var htmlDirectory = Path.Combine(destination, "html");
        var presentationDirectory = Path.Combine(destination, "presentation");
        Directory.CreateDirectory(htmlDirectory);
        Directory.CreateDirectory(presentationDirectory);
        File.Copy(
            Path.Combine(source, "html", "azure-night.json"),
            Path.Combine(htmlDirectory, "azure-night.json"));
        File.Copy(
            Path.Combine(
                source,
                "presentation",
                "professional-light.json"),
            Path.Combine(
                presentationDirectory,
                "professional-light.json"));
        return destination;
    }

    private static string FindBundledThemeDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "resources",
                "themes");
            if (File.Exists(
                    Path.Combine(
                        candidate,
                        "html",
                        "azure-night.json")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "The bundled theme definitions could not be located.");
    }

    private static JsonObject ReadTheme(string file)
    {
        return JsonNode.Parse(File.ReadAllText(file))?.AsObject()
            ?? throw new InvalidDataException(
                $"The test theme '{file}' does not contain an object.");
    }

    private static void WriteTheme(string file, JsonObject theme)
    {
        File.WriteAllText(
            file,
            theme.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
            }));
    }
}
