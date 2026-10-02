using System.Reflection;
using Avalonia.Controls;
using AzHST.Application.Abstractions;
using AzHST.Desktop.Models;
using AzHST.Desktop.ViewModels;
using AzHST.Desktop.Views;
using AzHST.Infrastructure;

namespace AzHST.Desktop.Services;

public sealed class AboutDialogService : IAboutDialogService
{
    private const string AboutResourceName = "AzHST.About.md";
    private const string ReleaseNotesResourceName = "AzHST.ReleaseNotes.md";

    private readonly Func<Window?> _ownerProvider;
    private readonly EmbeddedMarkdownDocumentLoader _documentLoader;
    private readonly Assembly _resourceAssembly;
    private readonly ApplicationIdentity _applicationIdentity;
    private readonly WebViewAvailability _webViewAvailability;
    private readonly IExternalBrowser _browser;

    public AboutDialogService(
        Func<Window?> ownerProvider,
        EmbeddedMarkdownDocumentLoader documentLoader,
        Assembly resourceAssembly,
        ApplicationIdentity applicationIdentity,
        WebViewAvailability webViewAvailability,
        IExternalBrowser browser)
    {
        _ownerProvider = ownerProvider;
        _documentLoader = documentLoader;
        _resourceAssembly = resourceAssembly;
        _applicationIdentity = applicationIdentity;
        _webViewAvailability = webViewAvailability;
        _browser = browser;
    }

    public Task ShowAsync()
    {
        var owner = _ownerProvider()
            ?? throw new InvalidOperationException(
                "The main window is not available.");
        var about = _documentLoader.Load(
            _resourceAssembly,
            AboutResourceName,
            $"About {_applicationIdentity.Name}");
        var releaseNotes = _documentLoader.Load(
            _resourceAssembly,
            ReleaseNotesResourceName,
            $"{_applicationIdentity.Name} release notes");
        var window = new AboutWindow
        {
            DataContext = new AboutWindowViewModel(
                _applicationIdentity,
                about,
                releaseNotes,
                _webViewAvailability,
                _browser),
        };

        return window.ShowDialog(owner);
    }
}
