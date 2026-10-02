using System.Text.Json;
using AzHST.Application.Models;

namespace AzHST.Infrastructure.Tests;

public sealed class JsonSettingsRepositoryTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "AzHST.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_AppliesThemeDefaultsToLegacySettings()
    {
        var paths = CreatePaths();
        Directory.CreateDirectory(paths.DataDirectory);
        await File.WriteAllTextAsync(
            paths.SettingsFile,
            JsonSerializer.Serialize(new
            {
                model = "gpt-5",
                outputDirectory = paths.GeneratedPagesDirectory,
                openResultsInExternalBrowser = true,
            }));
        var repository = new JsonSettingsRepository(paths);

        var settings = await repository.LoadAsync();

        Assert.Equal("gpt-5", settings.Model);
        Assert.Equal(OutputThemeSettings.DefaultHtmlThemeId, settings.Themes.HtmlThemeId);
        Assert.Equal(
            OutputThemeSettings.DefaultPresentationThemeId,
            settings.Themes.PresentationThemeId);
    }

    [Fact]
    public async Task LoadAsync_NormalizesBlankThemeIds()
    {
        var paths = CreatePaths();
        Directory.CreateDirectory(paths.DataDirectory);
        await File.WriteAllTextAsync(
            paths.SettingsFile,
            JsonSerializer.Serialize(new
            {
                model = " ",
                outputDirectory = paths.GeneratedPagesDirectory,
                themes = new
                {
                    htmlThemeId = " ",
                    presentationThemeId = "",
                },
            }));
        var repository = new JsonSettingsRepository(paths);

        var settings = await repository.LoadAsync();

        Assert.Equal(CopilotModelSelection.Automatic, settings.Model);
        Assert.Equal(OutputThemeSettings.DefaultHtmlThemeId, settings.Themes.HtmlThemeId);
        Assert.Equal(
            OutputThemeSettings.DefaultPresentationThemeId,
            settings.Themes.PresentationThemeId);
    }

    [Fact]
    public async Task SaveAsync_RoundTripsSelectedThemeIds()
    {
        var paths = CreatePaths();
        var repository = new JsonSettingsRepository(paths);
        var expected = new AppSettings
        {
            Model = "claude-sonnet-4.5",
            OutputDirectory = paths.GeneratedPagesDirectory,
            OpenResultsInExternalBrowser = true,
            Themes = new OutputThemeSettings
            {
                HtmlThemeId = "custom-html",
                PresentationThemeId = "custom-presentation",
            },
        };

        await repository.SaveAsync(expected);
        var actual = await repository.LoadAsync();
        var persistedJson = await File.ReadAllTextAsync(paths.SettingsFile);

        Assert.Equal("claude-sonnet-4.5", actual.Model);
        Assert.Equal("custom-html", actual.Themes.HtmlThemeId);
        Assert.Equal(
            "custom-presentation",
            actual.Themes.PresentationThemeId);
        Assert.Contains(
            "\"model\": \"claude-sonnet-4.5\"",
            persistedJson,
            StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private ApplicationPaths CreatePaths()
    {
        var dataDirectory = Path.Combine(_testDirectory, "data");
        return new ApplicationPaths(
            dataDirectory,
            Path.Combine(dataDirectory, "settings.json"),
            Path.Combine(_testDirectory, "generated"),
            Path.Combine(dataDirectory, "copilot"),
            Path.Combine(_testDirectory, "database", "azhst.db"));
    }
}
