using AzHST.Application.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AzHST.Desktop.ViewModels;

public sealed partial class SettingsWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _model;

    [ObservableProperty]
    private string _outputDirectory;

    [ObservableProperty]
    private bool _openResultsInExternalBrowser;

    [ObservableProperty]
    private OutputThemeOption _selectedHtmlTheme;

    [ObservableProperty]
    private OutputThemeOption _selectedPresentationTheme;

    public SettingsWindowViewModel(
        AppSettings settings,
        IReadOnlyList<OutputThemeOption> htmlThemeOptions,
        IReadOnlyList<OutputThemeOption> presentationThemeOptions)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(htmlThemeOptions);
        ArgumentNullException.ThrowIfNull(presentationThemeOptions);

        if (htmlThemeOptions.Count == 0)
        {
            throw new ArgumentException(
                "At least one HTML theme is required.",
                nameof(htmlThemeOptions));
        }

        if (presentationThemeOptions.Count == 0)
        {
            throw new ArgumentException(
                "At least one presentation theme is required.",
                nameof(presentationThemeOptions));
        }

        _model = settings.Model;
        _outputDirectory = settings.OutputDirectory;
        _openResultsInExternalBrowser = settings.OpenResultsInExternalBrowser;
        HtmlThemeOptions = htmlThemeOptions;
        PresentationThemeOptions = presentationThemeOptions;
        _selectedHtmlTheme = FindSelectedTheme(
            htmlThemeOptions,
            settings.Themes.HtmlThemeId);
        _selectedPresentationTheme = FindSelectedTheme(
            presentationThemeOptions,
            settings.Themes.PresentationThemeId);
    }

    public IReadOnlyList<OutputThemeOption> HtmlThemeOptions { get; }

    public IReadOnlyList<OutputThemeOption> PresentationThemeOptions { get; }

    public AppSettings ToSettings()
    {
        return new AppSettings
        {
            Model = Model,
            OutputDirectory = OutputDirectory,
            OpenResultsInExternalBrowser = OpenResultsInExternalBrowser,
            Themes = new OutputThemeSettings
            {
                HtmlThemeId = SelectedHtmlTheme.Id,
                PresentationThemeId = SelectedPresentationTheme.Id,
            },
        };
    }

    private static OutputThemeOption FindSelectedTheme(
        IReadOnlyList<OutputThemeOption> options,
        string selectedId)
    {
        return options.FirstOrDefault(
                option => string.Equals(
                    option.Id,
                    selectedId,
                    StringComparison.Ordinal))
            ?? options[0];
    }
}
