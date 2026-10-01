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

    public SettingsDialogService(
        Func<Window?> ownerProvider,
        IOutputThemeCatalog themes)
    {
        _ownerProvider = ownerProvider;
        _themes = themes;
    }

    public Task<AppSettings?> ShowAsync(AppSettings currentSettings)
    {
        var owner = _ownerProvider()
            ?? throw new InvalidOperationException("The main window is not available.");

        var window = new SettingsWindow
        {
            DataContext = new SettingsWindowViewModel(
                currentSettings,
                _themes.HtmlThemes,
                _themes.PresentationThemes),
        };

        return window.ShowDialog<AppSettings?>(owner);
    }
}
