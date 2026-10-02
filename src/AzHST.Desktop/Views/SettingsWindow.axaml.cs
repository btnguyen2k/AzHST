using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AzHST.Application.Models;
using AzHST.Desktop.ViewModels;

namespace AzHST.Desktop.Views;

public sealed partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        var settings = (DataContext as SettingsWindowViewModel)?.ToSettings()
            ?? new AppSettings();
        Close(settings);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
