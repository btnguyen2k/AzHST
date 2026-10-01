using Avalonia.Controls;
using AzHST.Application.Abstractions;
using AzHST.Application.Models;
using AzHST.Desktop.ViewModels;
using AzHST.Desktop.Views;

namespace AzHST.Desktop.Services;

public sealed class SettingsDialogService : ISettingsDialogService
{
    private readonly Func<Window?> _ownerProvider;
    private readonly IOutputThemeCatalog _themes;
    private readonly ICopilotModelCatalog _modelCatalog;

    public SettingsDialogService(
        Func<Window?> ownerProvider,
        IOutputThemeCatalog themes,
        ICopilotModelCatalog modelCatalog)
    {
        _ownerProvider = ownerProvider;
        _themes = themes;
        _modelCatalog = modelCatalog;
    }

    public async Task<AppSettings?> ShowAsync(AppSettings currentSettings)
    {
        var owner = _ownerProvider()
            ?? throw new InvalidOperationException("The main window is not available.");

        var viewModel = new SettingsWindowViewModel(
            currentSettings,
            _themes.HtmlThemes,
            _themes.PresentationThemes);
        var window = new SettingsWindow
        {
            DataContext = viewModel,
        };

        using var cancellation = new CancellationTokenSource();
        var dialogTask = window.ShowDialog<AppSettings?>(owner);
        var modelLoadingTask = viewModel.LoadCopilotModelsAsync(
            _modelCatalog,
            cancellation.Token);

        try
        {
            return await dialogTask;
        }
        finally
        {
            await cancellation.CancelAsync();
            await modelLoadingTask;
        }
    }
}
