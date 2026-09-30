using System.Text.RegularExpressions;
using AzHST.Application.Exceptions;

namespace AzHST.Application.Services;

public sealed partial class GeneratedHtmlDocumentProcessor
{
    public const int MaximumDocumentLength = 4 * 1024 * 1024;

    private const string SecurityMetadata = """
        <meta http-equiv="Content-Security-Policy" content="default-src 'none'; base-uri 'none'; connect-src 'none'; font-src data:; form-action 'none'; frame-src 'none'; img-src data: blob:; media-src data: blob:; object-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'">
        <meta name="referrer" content="no-referrer">
        """;

    public string Process(string modelResponse)
    {
        if (string.IsNullOrWhiteSpace(modelResponse))
        {
            throw new VisualizationGenerationException("Copilot returned an empty response.");
        }

        var html = ExtractHtml(modelResponse);
        if (html.Length > MaximumDocumentLength)
        {
            throw new VisualizationGenerationException(
                $"The generated page exceeds the {MaximumDocumentLength / (1024 * 1024)} MB safety limit.");
        }

        if (!HtmlElementRegex().IsMatch(html)
            || !HeadElementRegex().IsMatch(html)
            || !BodyElementRegex().IsMatch(html))
        {
            throw new VisualizationGenerationException(
                "Copilot did not return a complete HTML document. Try generating the visualization again.");
        }

        if (ExternalReferenceRegex().IsMatch(html) || ExternalCssUrlRegex().IsMatch(html))
        {
            throw new VisualizationGenerationException(
                "The generated page references external content. Regenerate it as a self-contained visualization.");
        }

        html = ExistingContentSecurityPolicyRegex().Replace(html, string.Empty);
        html = BaseElementRegex().Replace(html, string.Empty);

        var head = HeadElementRegex().Match(html);
        return html.Insert(head.Index + head.Length, $"{Environment.NewLine}{SecurityMetadata}");
    }

    private static string ExtractHtml(string response)
    {
        var candidate = response.Trim();
        var fencedBlock = HtmlCodeFenceRegex().Match(candidate);
        if (fencedBlock.Success)
        {
            candidate = fencedBlock.Groups["html"].Value.Trim();
        }

        var start = candidate.IndexOf("<!doctype html", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            start = candidate.IndexOf("<html", StringComparison.OrdinalIgnoreCase);
        }

        if (start < 0)
        {
            throw new VisualizationGenerationException(
                "Copilot's response did not contain an HTML document.");
        }

        var endMarkerIndex = candidate.LastIndexOf("</html>", StringComparison.OrdinalIgnoreCase);
        if (endMarkerIndex < start)
        {
            throw new VisualizationGenerationException(
                "Copilot returned an incomplete HTML document.");
        }

        var html = candidate[start..(endMarkerIndex + "</html>".Length)].Trim();
        if (!html.StartsWith("<!doctype html", StringComparison.OrdinalIgnoreCase))
        {
            html = $"<!doctype html>{Environment.NewLine}{html}";
        }

        return html;
    }

    [GeneratedRegex(
        """```(?:html)?\s*(?<html>[\s\S]*?)\s*```""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlCodeFenceRegex();

    [GeneratedRegex(
        """<html\b[^>]*>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlElementRegex();

    [GeneratedRegex(
        """<head\b[^>]*>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HeadElementRegex();

    [GeneratedRegex(
        """<body\b[^>]*>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BodyElementRegex();

    [GeneratedRegex(
        """(?:src|href)\s*=\s*["']\s*(?:https?:|file:|ftp:|//|javascript:)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExternalReferenceRegex();

    [GeneratedRegex(
        """url\(\s*["']?\s*(?:https?:|file:|ftp:|//)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExternalCssUrlRegex();

    [GeneratedRegex(
        """<meta\b[^>]*http-equiv\s*=\s*["']?\s*content-security-policy\s*["']?[^>]*>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExistingContentSecurityPolicyRegex();

    [GeneratedRegex(
        """<base\b[^>]*>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BaseElementRegex();
}
