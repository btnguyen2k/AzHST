using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AzHST.Desktop.ViewModels;

namespace AzHST.Desktop.Views;

public sealed partial class AboutWindow : Window
{
    private readonly Dictionary<NativeWebView, string> _loadedDocuments = [];
    private readonly HashSet<NativeWebView> _loadedWebViews = [];
    private readonly HashSet<NativeWebView> _pendingDocumentLoads = [];

    public AboutWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void DocumentationWebView_Loaded(
        object? sender,
        RoutedEventArgs e)
    {
        if (sender is NativeWebView webView)
        {
            _loadedWebViews.Add(webView);
            NavigateToCurrentDocument(webView);
        }
    }

    private void DocumentationWebView_DataContextChanged(
        object? sender,
        EventArgs e)
    {
        if (sender is NativeWebView webView
            && _loadedWebViews.Contains(webView))
        {
            NavigateToCurrentDocument(webView);
        }
    }

    private void DocumentationWebView_Unloaded(
        object? sender,
        RoutedEventArgs e)
    {
        if (sender is not NativeWebView webView)
        {
            return;
        }

        _loadedWebViews.Remove(webView);
        _loadedDocuments.Remove(webView);
        _pendingDocumentLoads.Remove(webView);
    }

    private void NavigateToCurrentDocument(NativeWebView webView)
    {
        var html = webView.DataContext as string ?? webView.Tag as string;
        if (string.IsNullOrWhiteSpace(html)
            || (_loadedDocuments.TryGetValue(webView, out var currentHtml)
                && string.Equals(currentHtml, html, StringComparison.Ordinal)))
        {
            return;
        }

        _loadedDocuments[webView] = html;
        _pendingDocumentLoads.Add(webView);
        try
        {
            webView.NavigateToString(html);
        }
        catch
        {
            _loadedDocuments.Remove(webView);
            _pendingDocumentLoads.Remove(webView);
            throw;
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

        if (sender is NativeWebView webView
            && e.Request.Scheme == "data"
            && _pendingDocumentLoads.Remove(webView))
        {
            return;
        }

        if (sender is NativeWebView blockedWebView)
        {
            _pendingDocumentLoads.Remove(blockedWebView);
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
