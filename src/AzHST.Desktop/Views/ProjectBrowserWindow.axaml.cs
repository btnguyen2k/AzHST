using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AzHST.Desktop.ViewModels;

namespace AzHST.Desktop.Views;

public sealed partial class ProjectBrowserWindow : Window
{
    public ProjectBrowserWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void Search_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter
            && DataContext is ProjectBrowserViewModel viewModel
            && viewModel.SearchCommand.CanExecute(null))
        {
            viewModel.SearchCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void Projects_DoubleTapped(
        object? sender,
        TappedEventArgs e)
    {
        if (DataContext is ProjectBrowserViewModel viewModel
            && viewModel.OpenSelectedCommand.CanExecute(null))
        {
            viewModel.OpenSelectedCommand.Execute(null);
            e.Handled = true;
        }
    }
}
