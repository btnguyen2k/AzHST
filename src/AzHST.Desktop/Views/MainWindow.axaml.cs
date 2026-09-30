using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AzHST.Desktop.ViewModels;

namespace AzHST.Desktop.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void WebView_NavigationStarted(
        object? sender,
        WebViewNavigationStartingEventArgs e)
    {
        if (e.Request is null || e.Request.AbsoluteUri == "about:blank")
        {
            return;
        }

        if (DataContext is MainWindowViewModel viewModel
            && IsSameLocalDocument(e.Request, viewModel.PreviewUri))
        {
            return;
        }

        e.Cancel = true;
        if (DataContext is MainWindowViewModel blockedViewModel)
        {
            blockedViewModel.StatusMessage = "Blocked navigation outside the generated local page.";
        }
    }

    private void WebView_NewWindowRequested(
        object? sender,
        WebViewNewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.StatusMessage = "Blocked the generated page from opening a new window.";
        }
    }

    private static bool IsSameLocalDocument(Uri request, Uri? allowedDocument)
    {
        if (!request.IsFile || allowedDocument is null || !allowedDocument.IsFile)
        {
            return false;
        }

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(
            Path.GetFullPath(request.LocalPath),
            Path.GetFullPath(allowedDocument.LocalPath),
            comparison);
    }
}
