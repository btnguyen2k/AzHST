using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using AzHST.Application.Abstractions;
using AzHST.Application.Exceptions;
using AzHST.Application.Models;

namespace AzHST.Application.Services;

public sealed partial class GeneratedHtmlDocumentProcessor
{
    public const int MaximumDocumentLength = 4 * 1024 * 1024;
    public const int MaximumAzureIconCount = 32;

    private const string SecurityMetadata = """
        <meta http-equiv="Content-Security-Policy" content="default-src 'none'; base-uri 'none'; connect-src 'none'; font-src data:; form-action 'none'; frame-src 'none'; img-src data: blob:; media-src data: blob:; object-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'">
        <meta name="referrer" content="no-referrer">
        """;

    private static readonly PresentationPlanHtmlManifest PresentationPlanManifest =
        new();

    private readonly IAzureIconCatalog _azureIcons;

    public GeneratedHtmlDocumentProcessor(IAzureIconCatalog? azureIcons = null)
    {
        _azureIcons = azureIcons ?? EmptyAzureIconCatalog.Instance;
    }

    public string Process(
        string modelResponse,
        HtmlThemeDefinition? theme = null)
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

        var plan = ValidatePresentationNarrative(html);
        ValidateSourceLinks(html, plan);
        var referenceScanHtml = SourceAnchorOpeningTagRegex().Replace(
            html,
            "<a>");
        if (ExternalReferenceRegex().IsMatch(referenceScanHtml)
            || ExternalCssUrlRegex().IsMatch(html))
        {
            throw new VisualizationGenerationException(
                "The generated page references external content. Regenerate it as a self-contained visualization.");
        }

        ValidateExperienceContract(html);

        html = ResolveAzureIconPlaceholders(html);
        if (html.Length > MaximumDocumentLength)
        {
            throw new VisualizationGenerationException(
                $"The generated page exceeds the {MaximumDocumentLength / (1024 * 1024)} MB safety limit after embedding Azure icons.");
        }

        html = ExistingContentSecurityPolicyRegex().Replace(html, string.Empty);
        html = BaseElementRegex().Replace(html, string.Empty);
        if (theme is not null)
        {
            html = ApplyTheme(html, theme);
        }

        var head = HeadElementRegex().Match(html);
        html = html.Insert(
            head.Index + head.Length,
            $"{Environment.NewLine}{SecurityMetadata}");
        if (html.Length > MaximumDocumentLength)
        {
            throw new VisualizationGenerationException(
                $"The generated page exceeds the {MaximumDocumentLength / (1024 * 1024)} MB safety limit after final processing.");
        }

        return html;
    }

    private static PresentationPlan ValidatePresentationNarrative(string html)
    {
        try
        {
            return PresentationPlanManifest.ExtractRequired(html);
        }
        catch (InvalidDataException exception)
        {
            throw new VisualizationGenerationException(
                "Copilot returned a page without a valid PowerPoint narrative. Try generating the visualization again.",
                exception);
        }
    }

    private static void ValidateSourceLinks(
        string html,
        PresentationPlan plan)
    {
        var sources = plan.Sources ?? [];
        if (sources.Count is < 1 or > PresentationSourcePolicy.MaximumSourceCount)
        {
            throw new VisualizationGenerationException(
                $"The generated page must contain 1-{PresentationSourcePolicy.MaximumSourceCount} linked sources.");
        }

        var expectedSources =
            new List<KeyValuePair<string, string>>(sources.Count);
        var expectedUrls = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            if (source is null
                || string.IsNullOrWhiteSpace(source.Title)
                || source.Title.Trim().Length
                    > PresentationSourcePolicy.MaximumTitleLength
                || !PresentationSourcePolicy.TryNormalizeUrl(
                    source.Url,
                    out var normalizedUrl)
                || !expectedUrls.Add(normalizedUrl))
            {
                throw new VisualizationGenerationException(
                    "The generated page contains an invalid or duplicate presentation source.");
            }

            expectedSources.Add(new KeyValuePair<string, string>(
                normalizedUrl,
                source.Title.Trim()));
        }

        var linkedSourceCount = 0;
        foreach (Match anchor in AnchorElementRegex().Matches(html))
        {
            var attributes = anchor.Groups["attributes"].Value;
            if (!SourceMarkerAttributeRegex().IsMatch(attributes))
            {
                continue;
            }

            var href = HrefAttributeRegex().Match(attributes);
            var rel = RelAttributeRegex().Match(attributes);
            var relTokens = rel.Success
                ? WebUtility.HtmlDecode(rel.Groups["value"].Value)
                    .Split(
                        (char[]?)null,
                        StringSplitOptions.RemoveEmptyEntries
                            | StringSplitOptions.TrimEntries)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)
                : [];
            var sourceTitle = NormalizeAnchorText(
                anchor.Groups["content"].Value);

            if (!href.Success
                || !TargetBlankAttributeRegex().IsMatch(attributes)
                || !relTokens.Contains("noopener")
                || !relTokens.Contains("noreferrer")
                || !PresentationSourcePolicy.TryNormalizeUrl(
                    WebUtility.HtmlDecode(href.Groups["value"].Value),
                    out var normalizedUrl)
                || linkedSourceCount >= expectedSources.Count
                || !string.Equals(
                    normalizedUrl,
                    expectedSources[linkedSourceCount].Key,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    sourceTitle,
                    expectedSources[linkedSourceCount].Value,
                    StringComparison.Ordinal))
            {
                throw new VisualizationGenerationException(
                    "The generated Sources section contains an invalid or mismatched link.");
            }

            linkedSourceCount++;
        }

        if (SourceAnchorOpeningTagRegex().Matches(html).Count
                != linkedSourceCount
            || linkedSourceCount != expectedSources.Count)
        {
            throw new VisualizationGenerationException(
                "The generated Sources section does not match the presentation sources.");
        }
    }

    private static string NormalizeAnchorText(string value)
    {
        var text = HtmlElementTagRegex().Replace(value, " ");
        return WhitespaceRegex().Replace(
                WebUtility.HtmlDecode(text),
                " ")
            .Trim();
    }

    private static void ValidateExperienceContract(string html)
    {
        var hasInlineScript = false;
        foreach (Match match in InlineScriptRegex().Matches(html))
        {
            if (!PresentationPlanManifest.IsManifestAttributes(
                    match.Groups["attributes"].Value)
                && !string.IsNullOrWhiteSpace(match.Groups["content"].Value))
            {
                hasInlineScript = true;
                break;
            }
        }

        if (!hasInlineScript || !InteractiveControlRegex().IsMatch(html))
        {
            throw new VisualizationGenerationException(
                "Copilot returned a static page without the required interactive controls. Try generating the visualization again.");
        }

        if (!PurposefulMotionRegex().IsMatch(html))
        {
            throw new VisualizationGenerationException(
                "Copilot returned a page without the required animation or motion. Try generating the visualization again.");
        }

        if (!ReducedMotionRegex().IsMatch(html))
        {
            throw new VisualizationGenerationException(
                "Copilot returned an animated page without reduced-motion support. Try generating the visualization again.");
        }
    }

    private string ResolveAzureIconPlaceholders(string html)
    {
        var placeholders = AzureIconPlaceholderRegex().Matches(html);
        if (placeholders.Count > MaximumAzureIconCount)
        {
            throw new VisualizationGenerationException(
                $"The generated page uses more than {MaximumAzureIconCount} Azure icons.");
        }

        var resolved = AzureIconPlaceholderRegex().Replace(
            html,
            ResolveAzureIconPlaceholder);

        if (AzureIconAttributeRegex().IsMatch(resolved))
        {
            throw new VisualizationGenerationException(
                "Copilot returned a malformed Azure icon placeholder. Try generating the visualization again.");
        }

        return resolved;
    }

    private string ResolveAzureIconPlaceholder(Match match)
    {
        if (SourceAttributeRegex().IsMatch(match.Value))
        {
            throw new VisualizationGenerationException(
                "Azure icon placeholders must not provide their own image source.");
        }

        var alt = AltAttributeRegex().Match(match.Value);
        if (!alt.Success
            || string.IsNullOrWhiteSpace(
                WebUtility.HtmlDecode(alt.Groups["value"].Value)))
        {
            throw new VisualizationGenerationException(
                "Every Azure icon must include a descriptive alt attribute.");
        }

        var key = match.Groups["key"].Value;
        if (!_azureIcons.TryGetDataUri(key, out var dataUri))
        {
            throw new VisualizationGenerationException(
                $"Copilot requested the unavailable Azure icon '{key}'. Try generating the visualization again.");
        }

        var iconAttribute = match.Groups["iconAttribute"];
        var relativeIndex = iconAttribute.Index - match.Index;
        var replacement =
            $"data-azure-icon-resolved=\"{key}\" src=\"{dataUri}\"";

        return match.Value.Remove(relativeIndex, iconAttribute.Length)
            .Insert(relativeIndex, replacement);
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

    private static string ApplyTheme(
        string html,
        HtmlThemeDefinition theme)
    {
        html = ExistingOutputThemeStyleRegex().Replace(html, string.Empty);

        var htmlElement = HtmlElementRegex().Match(html);
        var openingTag = DataThemeAttributeRegex().Replace(
            htmlElement.Value,
            string.Empty);
        openingTag = openingTag.Insert(
            openingTag.Length - 1,
            $" data-azh-theme=\"{theme.Id}\"");
        html = html
            .Remove(htmlElement.Index, htmlElement.Length)
            .Insert(htmlElement.Index, openingTag);

        var closingHead = HeadClosingElementRegex().Match(html);
        if (!closingHead.Success)
        {
            throw new VisualizationGenerationException(
                "Copilot did not return a complete HTML head element.");
        }

        return html.Insert(
            closingHead.Index,
            $"{Environment.NewLine}{BuildThemeStyle(theme)}{Environment.NewLine}");
    }

    private static string BuildThemeStyle(HtmlThemeDefinition theme)
    {
        var palette = theme.Palette;
        var typography = theme.Typography;
        var appearance = theme.Appearance;
        var motion = theme.Motion;
        var colorScheme = appearance.ColorScheme == HtmlColorScheme.Dark
            ? "dark"
            : "light";
        var lineHeight = typography.LineHeight.ToString(
            "0.##",
            CultureInfo.InvariantCulture);

        return $$"""
            <style id="azh-output-theme">
              :root {
                --azh-page: {{palette.Page}};
                --azh-surface: {{palette.Surface}};
                --azh-surface-raised: {{palette.SurfaceRaised}};
                --azh-surface-selected: {{palette.SurfaceSelected}};
                --azh-border: {{palette.Border}};
                --azh-text: {{palette.Text}};
                --azh-text-muted: {{palette.TextMuted}};
                --azh-primary: {{palette.Primary}};
                --azh-flow-active: {{palette.ActiveFlow}};
                --azh-accent: {{palette.Accent}};
                --azh-success: {{palette.Success}};
                --azh-warning: {{palette.Warning}};
                --azh-danger: {{palette.Danger}};
                --azh-focus: {{palette.Focus}};
                --azh-icon-tile: {{palette.IconTile}};
                --azh-font-family: {{typography.FontFamily}};
                --azh-base-size: {{typography.BaseSizePixels}}px;
                --azh-line-height: {{lineHeight}};
                --azh-corner-radius: {{appearance.CornerRadiusPixels}}px;
                --azh-icon-tile-size: {{appearance.IconTileSizePixels}}px;
                --azh-transition-duration: {{motion.TransitionDurationMilliseconds}}ms;
                --azh-sequence-step-duration: {{motion.SequenceStepDurationMilliseconds}}ms;
              }

              html[data-azh-theme="{{theme.Id}}"] {
                color-scheme: {{colorScheme}};
                background: var(--azh-page);
                font-size: var(--azh-base-size);
              }

              html[data-azh-theme="{{theme.Id}}"] body {
                color: var(--azh-text);
                background-color: var(--azh-page);
                font-family: var(--azh-font-family);
                line-height: var(--azh-line-height);
              }

              html[data-azh-theme="{{theme.Id}}"] :focus-visible {
                outline-color: var(--azh-focus);
              }
            </style>
            """;
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
        """</head\s*>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HeadClosingElementRegex();

    [GeneratedRegex(
        """<body\b[^>]*>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BodyElementRegex();

    [GeneratedRegex(
        """<script\b(?<attributes>[^>]*)>(?<content>[\s\S]*?)</script\s*>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InlineScriptRegex();

    [GeneratedRegex(
        """<(?:button|input|select)\b""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InteractiveControlRegex();

    [GeneratedRegex(
        """(?:@keyframes\b|\b(?:animation|transition)(?:-[a-z-]+)?\s*:(?!\s*(?:none\b|0(?:ms|s)?\b))|\.animate\s*\(|\brequestAnimationFrame\s*\()""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PurposefulMotionRegex();

    [GeneratedRegex(
        """prefers-reduced-motion\s*:\s*reduce""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReducedMotionRegex();

    [GeneratedRegex(
        """<img\b[^>]*?(?<iconAttribute>\bdata-azure-icon\s*=\s*(?<quote>["'])(?<key>[a-z0-9][a-z0-9/-]{0,119})\k<quote>)[^>]*>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AzureIconPlaceholderRegex();

    [GeneratedRegex(
        """\bdata-azure-icon\s*=""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AzureIconAttributeRegex();

    [GeneratedRegex(
        """\bsrc\s*=""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SourceAttributeRegex();

    [GeneratedRegex(
        """\balt\s*=\s*(?<quote>["'])(?<value>[^"']*)\k<quote>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AltAttributeRegex();

    [GeneratedRegex(
        """(?:src|href)\s*=\s*["']\s*(?:https?:|file:|ftp:|data:|//|javascript:|vbscript:)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExternalReferenceRegex();

    [GeneratedRegex(
        """<a\b(?<attributes>[^>]*)>(?<content>[\s\S]*?)</a\s*>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AnchorElementRegex();

    [GeneratedRegex(
        """<a\b(?=[^>]*\bdata-azh-source\b)[^>]*>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SourceAnchorOpeningTagRegex();

    [GeneratedRegex(
        """\bdata-azh-source(?:\s*=\s*(?:["'][^"']*["']|[^\s>]+))?""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SourceMarkerAttributeRegex();

    [GeneratedRegex(
        """\bhref\s*=\s*(?<quote>["'])(?<value>[^"']*)\k<quote>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HrefAttributeRegex();

    [GeneratedRegex(
        """\btarget\s*=\s*(?<quote>["'])_blank\k<quote>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TargetBlankAttributeRegex();

    [GeneratedRegex(
        """\brel\s*=\s*(?<quote>["'])(?<value>[^"']*)\k<quote>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RelAttributeRegex();

    [GeneratedRegex(
        """<[^>]+>""",
        RegexOptions.CultureInvariant)]
    private static partial Regex HtmlElementTagRegex();

    [GeneratedRegex(
        """\s+""",
        RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

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

    [GeneratedRegex(
        """\sdata-azh-theme\s*=\s*(?:"[^"]*"|'[^']*'|[^\s>]+)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DataThemeAttributeRegex();

    [GeneratedRegex(
        """<style\b[^>]*\bid\s*=\s*(?:["']azh-output-theme["']|azh-output-theme\b)[^>]*>[\s\S]*?</style\s*>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExistingOutputThemeStyleRegex();

    private sealed class EmptyAzureIconCatalog : IAzureIconCatalog
    {
        public static EmptyAzureIconCatalog Instance { get; } = new();

        public IReadOnlyList<AzureIconDescriptor> FindRelevant(
            string query,
            int maximumResults)
        {
            return [];
        }

        public bool TryGetDataUri(string key, out string dataUri)
        {
            dataUri = string.Empty;
            return false;
        }
    }
}
