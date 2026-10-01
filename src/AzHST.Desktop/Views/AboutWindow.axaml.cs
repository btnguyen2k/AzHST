using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AzHST.Desktop.ViewModels;

namespace AzHST.Desktop.Views;

public sealed partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private static void DocumentationWebView_Loaded(
        object? sender,
        RoutedEventArgs e)
    {
        if (sender is NativeWebView webView
            && webView.Tag is string html
            && !string.IsNullOrWhiteSpace(html))
        {
            webView.NavigateToString(html);
        }
    }

    private void DocumentationWebView_NavigationStarted(
        object? sender,
        WebViewNavigationStartingEventArgs e)
    {
        if (e.Request is null || e.Request.AbsoluteUri == "about:blank")
        {
            return;
        }

        e.Cancel = true;
        if (DataContext is not AboutWindowViewModel viewModel)
        {
            return;
        }

        if (e.Request.Scheme is "http" or "https")
        {
            viewModel.OpenExternalLink(e.Request);
            return;
        }

        viewModel.ReportBlockedNavigation(e.Request);
    }

    private void DocumentationWebView_NewWindowRequested(
        object? sender,
        WebViewNewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (e.Request is null
            || DataContext is not AboutWindowViewModel viewModel)
        {
            return;
        }

        if (e.Request.Scheme is "http" or "https")
        {
            viewModel.OpenExternalLink(e.Request);
            return;
        }

        viewModel.ReportBlockedNavigation(e.Request);
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
