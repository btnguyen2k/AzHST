using AzHST.Application.Abstractions;
using AzHST.Application.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AzHST.Desktop.ViewModels;

public sealed partial class SettingsWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _outputDirectory;

    [ObservableProperty]
    private bool _openResultsInExternalBrowser;

    [ObservableProperty]
    private IReadOnlyList<CopilotModelOption> _copilotModelOptions =
        [CopilotModelOption.Automatic];

    [ObservableProperty]
    private CopilotModelOption _selectedCopilotModel =
        CopilotModelOption.Automatic;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSelectCopilotModel))]
    private bool _isLoadingCopilotModels;

    [ObservableProperty]
    private string _copilotModelStatus =
        "Available models are loaded from the signed-in GitHub Copilot account.";

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

        _outputDirectory = settings.OutputDirectory;
        _openResultsInExternalBrowser = settings.OpenResultsInExternalBrowser;
        var selectedModelId = CopilotModelSelection.Normalize(settings.Model);
        if (!CopilotModelSelection.IsAutomatic(selectedModelId))
        {
            _selectedCopilotModel = new CopilotModelOption(
                selectedModelId,
                $"{selectedModelId} (checking availability...)");
            _copilotModelOptions =
                [CopilotModelOption.Automatic, _selectedCopilotModel];
        }

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

    public bool CanSelectCopilotModel => !IsLoadingCopilotModels;

    public async Task LoadCopilotModelsAsync(
        ICopilotModelCatalog modelCatalog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(modelCatalog);

        IsLoadingCopilotModels = true;
        CopilotModelStatus =
            "Loading models available to the signed-in GitHub Copilot account...";
        var selectedId = SelectedCopilotModel.Id;

        try
        {
            var availableModels = await modelCatalog.ListModelsAsync(
                cancellationToken);
            var options = new List<CopilotModelOption>(
                availableModels.Count + 1)
            {
                CopilotModelOption.Automatic,
            };
            options.AddRange(availableModels.Where(
                option => !CopilotModelSelection.IsAutomatic(option.Id)));
            CopilotModelOptions = options;

            var selected = options.FirstOrDefault(
                option => string.Equals(
                    option.Id,
                    selectedId,
                    StringComparison.OrdinalIgnoreCase));
            if (selected is null)
            {
                SelectedCopilotModel = CopilotModelOption.Automatic;
                CopilotModelStatus =
                    $"The previously selected model '{selectedId}' is no longer available. Automatic will be used.";
                return;
            }

            SelectedCopilotModel = selected;
            CopilotModelStatus = availableModels.Count == 0
                ? "No named models were returned. Automatic selection remains available."
                : $"{availableModels.Count} named models are available.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            CopilotModelStatus =
                $"Could not load available models. The current selection is preserved. {exception.Message}";
        }
        finally
        {
            IsLoadingCopilotModels = false;
        }
    }

    public AppSettings ToSettings()
    {
        return new AppSettings
        {
            Model = CopilotModelSelection.Normalize(SelectedCopilotModel.Id),
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
