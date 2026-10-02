using AzHST.Application.Abstractions;
using AzHST.Desktop.Models;
using AzHST.Desktop.Services;
using AzHST.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AzHST.Desktop.ViewModels;

public sealed partial class AboutWindowViewModel : ObservableObject
{
    private readonly IExternalBrowser _browser;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string _statusMessage;

    public AboutWindowViewModel(
        ApplicationIdentity applicationIdentity,
        EmbeddedMarkdownDocument about,
        EmbeddedMarkdownDocument releaseNotes,
        WebViewAvailability webViewAvailability,
        IExternalBrowser browser)
    {
        ArgumentNullException.ThrowIfNull(applicationIdentity);
        ArgumentNullException.ThrowIfNull(about);
        ArgumentNullException.ThrowIfNull(releaseNotes);
        ArgumentNullException.ThrowIfNull(webViewAvailability);
        ArgumentNullException.ThrowIfNull(browser);

        ApplicationName = applicationIdentity.Name;
        VersionText = $"Version {applicationIdentity.Version}";
        WindowTitle = $"About {applicationIdentity.Name}";
        Tabs =
        [
            new AboutTabViewModel(
                "About",
                about,
                webViewAvailability.IsAvailable),
            new AboutTabViewModel(
                "Changelog",
                releaseNotes,
                webViewAvailability.IsAvailable),
        ];
        _statusMessage = webViewAvailability.IsAvailable
            ? string.Empty
            : webViewAvailability.Message;
        _browser = browser;
    }

    public string WindowTitle { get; }

    public string ApplicationName { get; }

    public string VersionText { get; }

    public IReadOnlyList<AboutTabViewModel> Tabs { get; }

    public bool HasStatusMessage => StatusMessage.Length > 0;

    public void OpenExternalLink(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (uri.Scheme is not ("http" or "https"))
        {
            StatusMessage =
                $"Blocked unsupported documentation link: {uri.Scheme}.";
            return;
        }

        try
        {
            _browser.Open(uri);
            StatusMessage = string.Empty;
        }
        catch (Exception exception)
        {
            StatusMessage =
                $"Could not open the documentation link: {exception.Message}";
        }
    }

    public void ReportBlockedNavigation(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        StatusMessage =
            $"Blocked navigation to unsupported content: {uri.Scheme}.";
    }
}
