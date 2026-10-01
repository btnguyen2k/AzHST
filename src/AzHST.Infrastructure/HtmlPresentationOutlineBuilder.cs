using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace AzHST.Infrastructure;

public sealed class HtmlPresentationOutlineBuilder
{
    public const int MaximumOutlineLength = 60_000;

    private static readonly HashSet<string> IgnoredElements = new(
        ["script", "style", "noscript", "template"],
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

        switch (element.LocalName)
        {
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
                outline.Add("IMAGE", element.GetAttribute("alt"));
                return;
            case "button":
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
            outline.Add(
                "SECTION",
                element.GetAttribute("aria-label")
                    ?? element.GetAttribute("data-title"));
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
