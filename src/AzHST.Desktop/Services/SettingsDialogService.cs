using Avalonia.Controls;
using AzHST.Application.Models;
using AzHST.Desktop.ViewModels;
using AzHST.Desktop.Views;

namespace AzHST.Desktop.Services;

public sealed class SettingsDialogService : ISettingsDialogService
{
    private readonly Func<Window?> _ownerProvider;

    public SettingsDialogService(Func<Window?> ownerProvider)
    {
        _ownerProvider = ownerProvider;
    }

    public Task<AppSettings?> ShowAsync(AppSettings currentSettings)
    {
        var owner = _ownerProvider()
            ?? throw new InvalidOperationException("The main window is not available.");

        var window = new SettingsWindow
        {
            DataContext = new SettingsWindowViewModel(currentSettings),
        };

        return window.ShowDialog<AppSettings?>(owner);
    }
}
