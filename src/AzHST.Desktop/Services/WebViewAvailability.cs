using Avalonia.Platform;

namespace AzHST.Desktop.Services;

public sealed record WebViewAvailability(bool IsAvailable, string Message)
{
    public static WebViewAvailability Detect()
    {
        var adapterTypes = GetCandidateAdapters();
        foreach (var adapterType in adapterTypes)
        {
            var adapter = WebViewAdapterInfo.GetAdapterInfo(adapterType);
            var canEmbed = adapter.SupportedScenarios.HasFlag(WebViewEmbeddingScenario.NativeControlHost)
                || adapter.SupportedScenarios.HasFlag(WebViewEmbeddingScenario.OffscreenRenderer);

            if (adapter.IsInstalled && canEmbed)
            {
                return new WebViewAvailability(
                    true,
                    $"{adapter.Type} {adapter.Version}".Trim());
            }
        }

        return new WebViewAvailability(
            false,
            "No compatible embedded WebView runtime was found. Results will open in the default browser.");
    }

    private static IReadOnlyList<WebViewAdapterType> GetCandidateAdapters()
    {
        if (OperatingSystem.IsWindows())
        {
            return [WebViewAdapterType.WebView2, WebViewAdapterType.WebView1];
        }

        if (OperatingSystem.IsMacOS())
        {
            return [WebViewAdapterType.WkWebView];
        }

        if (OperatingSystem.IsLinux())
        {
            return [WebViewAdapterType.WpeWebKit, WebViewAdapterType.WebKitGtk];
        }

        return [];
    }
}
