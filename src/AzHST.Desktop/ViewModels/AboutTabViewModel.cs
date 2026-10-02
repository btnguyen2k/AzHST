using AzHST.Infrastructure;

namespace AzHST.Desktop.ViewModels;

public sealed class AboutTabViewModel
{
    public AboutTabViewModel(
        string title,
        EmbeddedMarkdownDocument document,
        bool isWebViewAvailable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(document);

        Title = title;
        Markdown = document.Markdown;
        HtmlSource = isWebViewAvailable ? document.Html : null;
        HasHtmlSource = isWebViewAvailable;
        ShowTextFallback = !isWebViewAvailable;
    }

    public string Title { get; }

    public string Markdown { get; }

    public string? HtmlSource { get; }

    public bool HasHtmlSource { get; }

    public bool ShowTextFallback { get; }
}
