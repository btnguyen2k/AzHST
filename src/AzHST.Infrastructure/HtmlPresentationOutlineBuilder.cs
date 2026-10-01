using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace AzHST.Infrastructure;

public sealed partial class HtmlPresentationOutlineBuilder
{
    public const int MaximumOutlineLength = 60_000;

    private static readonly HashSet<string> IgnoredElements = new(
        ["style", "noscript", "template"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ContainerElements = new(
        [
            "main",
            "section",
            "article",
            "aside",
            "header",
            "footer",
            "nav",
            "div",
            "figure",
        ],
        StringComparer.OrdinalIgnoreCase);

    public string Build(string html)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(html);

        var document = new HtmlParser().ParseDocument(html);
        var body = document.Body
            ?? throw new InvalidDataException(
                "The generated HTML visualization does not contain a body.");
        var outline = new OutlineCollector(MaximumOutlineLength);

        outline.Add("PAGE TITLE", document.Title);
        foreach (var element in body.Children)
        {
            Visit(element, outline);
            if (outline.IsFull)
            {
                break;
            }
        }

        if (outline.LineCount == 0)
        {
            throw new InvalidDataException(
                "The generated HTML visualization does not contain presentation content.");
        }

        return outline.ToString();
    }

    private static void Visit(IElement element, OutlineCollector outline)
    {
        if (outline.IsFull || IgnoredElements.Contains(element.LocalName))
        {
            return;
        }

        if (element.HasAttribute("data-node"))
        {
            AddVisualNode(element, outline);
            return;
        }

        switch (element.LocalName)
        {
            case "script":
                AddInteractionNarrative(element, outline);
                return;
            case "h1":
            case "h2":
            case "h3":
            case "h4":
            case "h5":
            case "h6":
                outline.Add(element.LocalName.ToUpperInvariant(), element.TextContent);
                return;
            case "p":
                outline.Add("TEXT", element.TextContent);
                return;
            case "li":
                outline.Add("ITEM", element.TextContent);
                return;
            case "dt":
                outline.Add("TERM", element.TextContent);
                return;
            case "dd":
                outline.Add("DETAIL", element.TextContent);
                return;
            case "table":
                AddTable(element, outline);
                return;
            case "svg":
                AddDiagramLabels(element, outline);
                return;
            case "img":
                AddImage(element, outline);
                return;
            case "button":
                AddControl(element, outline);
                return;
            case "summary":
            case "label":
                outline.Add("CONTROL", element.TextContent);
                return;
            case "select":
                outline.Add("CONTROL", element.TextContent);
                return;
            case "input":
                outline.Add(
                    "CONTROL",
                    element.GetAttribute("aria-label")
                        ?? element.GetAttribute("placeholder")
                        ?? element.GetAttribute("value"));
                return;
        }

        if (ContainerElements.Contains(element.LocalName))
        {
            outline.Add("SECTION", ResolveContainerLabel(element));
        }

        if (element.Children.Length == 0)
        {
            outline.Add("TEXT", element.TextContent);
            return;
        }

        foreach (var child in element.Children)
        {
            Visit(child, outline);
            if (outline.IsFull)
            {
                return;
            }
        }
    }

    private static void AddVisualNode(
        IElement element,
        OutlineCollector outline)
    {
        var values = new[]
        {
            element.GetAttribute("data-node"),
            element.QuerySelector("strong")?.TextContent
                ?? element.GetAttribute("aria-label"),
            element.QuerySelector("small")?.TextContent,
            element.QuerySelector("img")?.GetAttribute(
                "data-azure-icon-resolved")
                ?? element.QuerySelector("img")?.GetAttribute(
                    "data-azure-icon"),
        }
        .Select(Normalize)
        .Where(value => value.Length > 0);

        outline.Add("VISUAL NODE", string.Join(" | ", values));
    }

    private static void AddImage(
        IElement element,
        OutlineCollector outline)
    {
        var key = element.GetAttribute("data-azure-icon-resolved")
            ?? element.GetAttribute("data-azure-icon");
        var alt = element.GetAttribute("alt");
        outline.Add(
            "IMAGE",
            string.IsNullOrWhiteSpace(key)
                ? alt
                : $"{key} | {alt}");
    }

    private static void AddControl(
        IElement element,
        OutlineCollector outline)
    {
        var choice = element.GetAttribute("data-choice");
        var value = element.GetAttribute("data-value");
        if (!string.IsNullOrWhiteSpace(choice)
            && !string.IsNullOrWhiteSpace(value))
        {
            outline.Add(
                "SCENARIO OPTION",
                $"{choice}={value} | {element.TextContent}");
            return;
        }

        outline.Add("CONTROL", element.TextContent);
    }

    private static string? ResolveContainerLabel(IElement element)
    {
        var label = element.GetAttribute("aria-label")
            ?? element.GetAttribute("data-title");
        if (!string.IsNullOrWhiteSpace(label))
        {
            return label;
        }

        var labelledBy = element.GetAttribute("aria-labelledby");
        return string.IsNullOrWhiteSpace(labelledBy)
            ? null
            : element.Owner?.GetElementById(labelledBy)?.TextContent;
    }

    private static void AddInteractionNarrative(
        IElement script,
        OutlineCollector outline)
    {
        if (string.Equals(
                script.GetAttribute("type"),
                "application/json",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var source = script.TextContent;
        foreach (Match match in InteractionStepRegex().Matches(source))
        {
            var node = DecodeJavaScriptString(match.Groups["node"].Value);
            var title = DecodeJavaScriptString(match.Groups["title"].Value);
            var text = DecodeJavaScriptString(match.Groups["text"].Value);
            outline.Add(
                "INTERACTION STEP",
                $"{node} | {title} | {text}");
        }

        foreach (Match match in DynamicTextRegex().Matches(source))
        {
            var text = DecodeJavaScriptString(match.Groups["text"].Value);
            if (text.Length >= 20 && text.Contains(' '))
            {
                outline.Add("INTERACTION STATE", text);
            }
        }
    }

    private static string DecodeJavaScriptString(string value)
    {
        try
        {
            return JsonSerializer.Deserialize<string>($"\"{value}\"")
                ?? string.Empty;
        }
        catch (JsonException)
        {
            return value;
        }
    }

    private static void AddTable(IElement table, OutlineCollector outline)
    {
        foreach (var row in table.QuerySelectorAll("tr"))
        {
            var cells = row.Children
                .Where(cell => cell.LocalName is "th" or "td")
                .Select(cell => Normalize(cell.TextContent))
                .Where(text => text.Length > 0)
                .ToArray();

            if (cells.Length > 0)
            {
                outline.Add("TABLE ROW", string.Join(" | ", cells));
            }
        }
    }

    private static void AddDiagramLabels(
        IElement svg,
        OutlineCollector outline)
    {
        outline.Add("DIAGRAM", svg.GetAttribute("aria-label"));

        var labels = svg
            .QuerySelectorAll("title, desc, text")
            .Select(element => Normalize(element.TextContent))
            .Where(text => text.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (labels.Length > 0)
        {
            outline.Add("DIAGRAM LABELS", string.Join(" | ", labels));
        }
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join(
                " ",
                value.Split(
                    [' ', '\t', '\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries));
    }

    [GeneratedRegex(
        """\{\s*node\s*:\s*"(?<node>(?:\\.|[^"\\])*)"\s*,\s*title\s*:\s*"(?<title>(?:\\.|[^"\\])*)"\s*,\s*text\s*:\s*"(?<text>(?:\\.|[^"\\])*)"\s*\}""",
        RegexOptions.CultureInvariant)]
    private static partial Regex InteractionStepRegex();

    [GeneratedRegex(
        "(?:textContent|innerText)\\s*=\\s*\"(?<text>(?:\\\\.|[^\"\\\\])*)\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex DynamicTextRegex();

    private sealed class OutlineCollector(int maximumLength)
    {
        private const int MaximumLineLength = 600;

        private readonly StringBuilder _builder = new();
        private readonly HashSet<string> _lines = new(
            StringComparer.OrdinalIgnoreCase);

        public bool IsFull { get; private set; }

        public int LineCount => _lines.Count;

        public void Add(string label, string? value)
        {
            if (IsFull)
            {
                return;
            }

            var text = Normalize(value);
            if (text.Length == 0)
            {
                return;
            }

            if (text.Length > MaximumLineLength)
            {
                text = $"{text[..(MaximumLineLength - 3)]}...";
            }

            var line = $"{label}: {text}";
            if (!_lines.Add(line))
            {
                return;
            }

            var requiredLength = line.Length + Environment.NewLine.Length;
            var remainingLength = maximumLength - _builder.Length;
            if (requiredLength > remainingLength)
            {
                if (remainingLength > 5)
                {
                    _builder.Append(line.AsSpan(0, remainingLength - 4));
                    _builder.Append("...");
                }

                IsFull = true;
                return;
            }

            _builder.AppendLine(line);
        }

        public override string ToString()
        {
            return _builder.ToString().TrimEnd();
        }
    }
}
