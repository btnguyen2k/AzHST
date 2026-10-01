namespace AzHST.Application.Services;

public static class PresentationSourcePolicy
{
    public const int MaximumSourceCount = 10;
    public const int MaximumTitleLength = 180;
    public const int MaximumUrlLength = 2_048;

    public static bool TryNormalizeUrl(
        string? value,
        out string normalizedUrl)
    {
        normalizedUrl = string.Empty;
        var candidate = value?.Trim();
        if (string.IsNullOrEmpty(candidate)
            || candidate.Length > MaximumUrlLength
            || !Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrEmpty(uri.Host)
            || uri.UserInfo.Length > 0
            || !uri.IsDefaultPort
            || !IsApprovedHost(uri))
        {
            return false;
        }

        normalizedUrl = uri.AbsoluteUri;
        return true;
    }

    private static bool IsApprovedHost(Uri uri)
    {
        var host = uri.IdnHost;
        if (host.Equals("learn.microsoft.com", StringComparison.OrdinalIgnoreCase)
            || host.Equals("azure.microsoft.com", StringComparison.OrdinalIgnoreCase)
            || host.Equals("microsoft.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".microsoft.com", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var path = uri.AbsolutePath.TrimStart('/');
        return path.StartsWith("Azure/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("Azure-Samples/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("microsoft/", StringComparison.OrdinalIgnoreCase);
    }
}
