using System.Text.RegularExpressions;
using AzHST.Application.Abstractions;
using AzHST.Application.Exceptions;
using AzHST.Application.Models;

namespace AzHST.Application.Services;

public sealed partial class GeneratePresentationUseCase
{
    public const int MinimumSlideCount = 4;
    public const int MaximumSlideCount = 9;
    public const int MaximumBulletsPerSlide = 4;
    public const int MaximumNodesPerSlide = 10;
    public const int MaximumConnectionsPerSlide = 16;
    public const int MaximumDiagramNodesPerSlide = 6;
    public const int MaximumComparisonNodesPerSlide = 4;
    public const int MaximumCardNodesPerSlide = 6;

    public const int MaximumTitleLength = 120;
    public const int MaximumSubtitleLength = 240;
    public const int MaximumSectionTitleLength = 100;
    public const int MaximumSlideSubtitleLength = 160;
    public const int MaximumSummaryLength = 420;
    public const int MaximumCalloutLength = 280;
    public const int MaximumBulletLength = 180;
    public const int MaximumNodeLabelLength = 80;
    public const int MaximumNodeDetailLength = 220;
    public const int MaximumConnectionLabelLength = 60;
    private const int MaximumSourcesPerSlide =
        PresentationSourcePolicy.MaximumSourceCount;
    private const int MaximumSourceLength =
        PresentationSourcePolicy.MaximumTitleLength;

    private static readonly HashSet<string> SupportedKinds = new(
        ["content", "diagram", "comparison", "cards", "summary"],
        StringComparer.Ordinal);

    private static readonly HashSet<string> VisualKinds = new(
        ["diagram", "comparison", "cards"],
        StringComparer.Ordinal);

    private static readonly HashSet<string> SupportedNodeTones = new(
        ["primary", "accent", "success", "warning", "danger", "neutral"],
        StringComparer.Ordinal);

    private readonly ICopilotPresentationPlanner _planner;
    private readonly IPresentationBuilder _builder;
    private readonly IAzureIconCatalog _azureIcons;
    private readonly IOutputThemeCatalog _themes;

    public GeneratePresentationUseCase(
        ICopilotPresentationPlanner planner,
        IPresentationBuilder builder,
        IAzureIconCatalog azureIcons,
        IOutputThemeCatalog themes)
    {
        _planner = planner;
        _builder = builder;
        _azureIcons = azureIcons;
        _themes = themes;
    }

    public async Task<PresentationArtifact> ExecuteAsync(
        string query,
        VisualizationArtifact visualization,
        AppSettings settings,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(visualization);
        ArgumentNullException.ThrowIfNull(settings);

        var normalizedQuery = query?.Trim();
        if (string.IsNullOrEmpty(normalizedQuery))
        {
            throw new PresentationGenerationException(
                "The original visualization question is unavailable.");
        }

        if (normalizedQuery.Length > CreateProjectUseCase.MaximumQueryLength)
        {
            throw new PresentationGenerationException(
                $"The question exceeds {CreateProjectUseCase.MaximumQueryLength:N0} characters.");
        }

        if (string.IsNullOrWhiteSpace(visualization.Id)
            || string.IsNullOrWhiteSpace(visualization.DirectoryPath))
        {
            throw new PresentationGenerationException(
                "The visualization artifact is missing its ID or output directory.");
        }

        if (string.IsNullOrWhiteSpace(visualization.Html))
        {
            throw new PresentationGenerationException(
                "The generated HTML visualization is unavailable for PowerPoint planning.");
        }

        var theme = _themes.GetPresentationTheme(
            settings.Themes.PresentationThemeId);

        var rawPlan = await _planner.CreatePresentationPlanAsync(
            normalizedQuery,
            CopilotModelSelection.Normalize(settings.Model),
            visualization,
            progress,
            cancellationToken);
        var plan = ValidateAndNormalize(rawPlan);

        progress?.Report(new GenerationProgress(
            GenerationStage.BuildingPresentation,
            "Building the PowerPoint presentation..."));

        var artifact = await _builder.BuildAsync(
            plan,
            visualization,
            theme,
            cancellationToken);

        progress?.Report(new GenerationProgress(
            GenerationStage.Completed,
            "PowerPoint presentation ready."));

        return artifact;
    }

    private PresentationPlan ValidateAndNormalize(PresentationPlan? plan)
    {
        if (plan is null)
        {
            throw new PresentationGenerationException(
                "Copilot did not return a presentation plan.");
        }

        var title = RequireText(plan.Title, "presentation title", MaximumTitleLength);
        var subtitle = OptionalText(plan.Subtitle, MaximumSubtitleLength);
        var slides = plan.Slides
            ?? throw new PresentationGenerationException(
                "Copilot returned a presentation plan without slides.");

        if (slides.Count is < MinimumSlideCount or > MaximumSlideCount)
        {
            throw new PresentationGenerationException(
                $"The presentation plan must contain {MinimumSlideCount}-{MaximumSlideCount} content slides.");
        }

        var normalizedSlides = slides
            .Select((slide, index) => ValidateSlide(slide, index + 1))
            .ToList();
        var sources = ValidateSources(plan.Sources);
        if (sources.Count == 0)
        {
            sources = normalizedSlides
                .SelectMany(slide => slide.Sources)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(PresentationSourcePolicy.MaximumSourceCount)
                .Select(title => new PresentationSourcePlan
                {
                    Title = title,
                })
                .ToList();
        }

        if (!VisualKinds.Contains(normalizedSlides[0].Kind))
        {
            throw new PresentationGenerationException(
                "The first presentation content slide must be visual.");
        }

        var visualSlideCount = normalizedSlides.Count(
            slide => VisualKinds.Contains(slide.Kind));
        var minimumVisualSlideCount =
            ((normalizedSlides.Count * 2) + 2) / 3;
        if (visualSlideCount < minimumVisualSlideCount)
        {
            throw new PresentationGenerationException(
                $"At least {minimumVisualSlideCount} of the {normalizedSlides.Count} content slides must be visual.");
        }

        return new PresentationPlan
        {
            Title = title,
            Subtitle = subtitle,
            Sources = sources,
            Slides = normalizedSlides,
        };
    }

    private static List<PresentationSourcePlan> ValidateSources(
        List<PresentationSourcePlan>? sources)
    {
        sources ??= [];
        if (sources.Count > PresentationSourcePolicy.MaximumSourceCount)
        {
            throw new PresentationGenerationException(
                $"The presentation contains more than {PresentationSourcePolicy.MaximumSourceCount} sources.");
        }

        var normalized = new List<PresentationSourcePlan>(sources.Count);
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            if (source is null)
            {
                throw new PresentationGenerationException(
                    "The presentation contains a missing source.");
            }

            var title = RequireText(
                source.Title,
                "presentation source title",
                PresentationSourcePolicy.MaximumTitleLength);
            if (!PresentationSourcePolicy.TryNormalizeUrl(
                    source.Url,
                    out var url))
            {
                throw new PresentationGenerationException(
                    $"The presentation source '{title}' must use an approved Microsoft HTTPS URL.");
            }

            if (!urls.Add(url))
            {
                throw new PresentationGenerationException(
                    $"The presentation contains duplicate source URL '{url}'.");
            }

            normalized.Add(new PresentationSourcePlan
            {
                Title = title,
                Url = url,
            });
        }

        return normalized;
    }

    private PresentationSlidePlan ValidateSlide(
        PresentationSlidePlan? slide,
        int slideNumber)
    {
        if (slide is null)
        {
            throw new PresentationGenerationException(
                $"Presentation slide {slideNumber} is missing.");
        }

        var kind = slide.Kind?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!SupportedKinds.Contains(kind))
        {
            throw new PresentationGenerationException(
                $"Presentation slide {slideNumber} has unsupported kind '{slide.Kind}'.");
        }

        var title = RequireText(
            slide.Title,
            $"slide {slideNumber} title",
            MaximumTitleLength);
        var sectionTitle = OptionalText(
            slide.SectionTitle,
            MaximumSectionTitleLength);
        if (string.Equals(
            sectionTitle,
            title,
            StringComparison.OrdinalIgnoreCase))
        {
            sectionTitle = string.Empty;
        }
        var subtitle = OptionalText(
            slide.Subtitle,
            MaximumSlideSubtitleLength);
        var summary = OptionalText(
            slide.Summary,
            MaximumSummaryLength);
        var callout = OptionalText(
            slide.Callout,
            MaximumCalloutLength);
        var bullets = ValidateTextList(
            slide.Bullets,
            $"slide {slideNumber} bullets",
            MaximumBulletsPerSlide,
            MaximumBulletLength);
        var sources = ValidateTextList(
            slide.Sources,
            $"slide {slideNumber} sources",
            MaximumSourcesPerSlide,
            MaximumSourceLength);
        var nodes = ValidateNodes(slide.Nodes, slideNumber);
        var connections = ValidateConnections(
            slide.Connections,
            nodes,
            slideNumber);

        if (VisualKinds.Contains(kind))
        {
            if (nodes.Count < 2)
            {
                throw new PresentationGenerationException(
                    $"Visual slide {slideNumber} must contain at least two nodes.");
            }

            var maximumNodes = kind switch
            {
                "diagram" => MaximumDiagramNodesPerSlide,
                "comparison" => MaximumComparisonNodesPerSlide,
                _ => MaximumCardNodesPerSlide,
            };
            if (nodes.Count > maximumNodes)
            {
                throw new PresentationGenerationException(
                    $"Presentation {kind} slide {slideNumber} must contain no more than {maximumNodes} nodes.");
            }

            if (bullets.Count > 0)
            {
                throw new PresentationGenerationException(
                    $"Visual slide {slideNumber} must use node details instead of bullet points.");
            }

            if (kind != "diagram" && connections.Count > 0)
            {
                throw new PresentationGenerationException(
                    $"Visual card slide {slideNumber} must not contain connections.");
            }

            if (kind == "diagram" && connections.Count == 0)
            {
                throw new PresentationGenerationException(
                    $"Diagram slide {slideNumber} must contain at least one connection.");
            }
        }
        else
        {
            if (nodes.Count > 0 || connections.Count > 0)
            {
                throw new PresentationGenerationException(
                    $"Content slide {slideNumber} must not contain visual nodes or connections.");
            }

            if (string.IsNullOrEmpty(summary) && bullets.Count == 0)
            {
                throw new PresentationGenerationException(
                    $"Content slide {slideNumber} must contain a summary or bullet points.");
            }
        }

        return new PresentationSlidePlan
        {
            Kind = kind,
            SectionTitle = sectionTitle,
            Title = title,
            Subtitle = subtitle,
            Summary = summary,
            Callout = callout,
            Bullets = bullets,
            Nodes = nodes,
            Connections = connections,
            Sources = sources,
        };
    }

    private List<PresentationNodePlan> ValidateNodes(
        List<PresentationNodePlan>? nodes,
        int slideNumber)
    {
        nodes ??= [];
        if (nodes.Count > MaximumNodesPerSlide)
        {
            throw new PresentationGenerationException(
                $"Presentation slide {slideNumber} contains more than {MaximumNodesPerSlide} nodes.");
        }

        var normalized = new List<PresentationNodePlan>(nodes.Count);
        var identifiers = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in nodes)
        {
            if (node is null)
            {
                throw new PresentationGenerationException(
                    $"Presentation slide {slideNumber} contains a missing node.");
            }

            var id = node.Id?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!NodeIdRegex().IsMatch(id) || !identifiers.Add(id))
            {
                throw new PresentationGenerationException(
                    $"Presentation slide {slideNumber} contains an invalid or duplicate node ID '{node.Id}'.");
            }

            var iconKey = node.IconKey?.Trim() ?? string.Empty;
            if (iconKey.Length > 0 && !_azureIcons.TryGetDataUri(iconKey, out _))
            {
                throw new PresentationGenerationException(
                    $"Presentation slide {slideNumber} uses unavailable Azure icon '{iconKey}'.");
            }

            var tone = string.IsNullOrWhiteSpace(node.Tone)
                ? "primary"
                : node.Tone.Trim().ToLowerInvariant();
            if (!SupportedNodeTones.Contains(tone))
            {
                throw new PresentationGenerationException(
                    $"Presentation slide {slideNumber} uses unsupported node tone '{node.Tone}'.");
            }

            normalized.Add(new PresentationNodePlan
            {
                Id = id,
                Label = RequireText(
                    node.Label,
                    $"slide {slideNumber} node label",
                    MaximumNodeLabelLength),
                Detail = OptionalText(
                    node.Detail,
                    MaximumNodeDetailLength),
                IconKey = iconKey,
                Tone = tone,
            });
        }

        return normalized;
    }

    private static List<PresentationConnectionPlan> ValidateConnections(
        List<PresentationConnectionPlan>? connections,
        IReadOnlyCollection<PresentationNodePlan> nodes,
        int slideNumber)
    {
        connections ??= [];
        if (connections.Count > MaximumConnectionsPerSlide)
        {
            throw new PresentationGenerationException(
                $"Presentation slide {slideNumber} contains more than {MaximumConnectionsPerSlide} connections.");
        }

        var nodeIds = nodes
            .Select(node => node.Id)
            .ToHashSet(StringComparer.Ordinal);
        var normalized = new List<PresentationConnectionPlan>(connections.Count);

        foreach (var connection in connections)
        {
            if (connection is null)
            {
                throw new PresentationGenerationException(
                    $"Presentation slide {slideNumber} contains a missing connection.");
            }

            var from = connection.From?.Trim().ToLowerInvariant() ?? string.Empty;
            var to = connection.To?.Trim().ToLowerInvariant() ?? string.Empty;
            if (from == to || !nodeIds.Contains(from) || !nodeIds.Contains(to))
            {
                throw new PresentationGenerationException(
                    $"Presentation slide {slideNumber} contains an invalid connection from '{connection.From}' to '{connection.To}'.");
            }

            normalized.Add(new PresentationConnectionPlan
            {
                From = from,
                To = to,
                Label = OptionalText(
                    connection.Label,
                    MaximumConnectionLabelLength),
            });
        }

        return normalized;
    }

    private static List<string> ValidateTextList(
        List<string>? values,
        string fieldName,
        int maximumCount,
        int maximumLength)
    {
        values ??= [];
        if (values.Count > maximumCount)
        {
            throw new PresentationGenerationException(
                $"{fieldName} contains more than {maximumCount} items.");
        }

        return values
            .Select((value, index) => RequireText(
                value,
                $"{fieldName} item {index + 1}",
                maximumLength))
            .ToList();
    }

    private static string RequireText(
        string? value,
        string fieldName,
        int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            throw new PresentationGenerationException(
                $"The {fieldName} is required.");
        }

        if (normalized.Length > maximumLength)
        {
            return TruncateWithEllipsis(normalized, maximumLength);
        }

        return normalized;
    }

    private static string OptionalText(
        string? value,
        int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length > maximumLength)
        {
            return TruncateWithEllipsis(normalized, maximumLength);
        }

        return normalized;
    }

    private static string TruncateWithEllipsis(
        string value,
        int maximumLength)
    {
        const char ellipsis = '…';

        var contentLength = maximumLength - 1;
        if (char.IsHighSurrogate(value[contentLength - 1])
            && char.IsLowSurrogate(value[contentLength]))
        {
            contentLength--;
        }

        var content = value[..contentLength].TrimEnd();
        var wordBoundary = content.LastIndexOfAny([' ', '\t', '\r', '\n']);
        if (wordBoundary >= maximumLength / 2)
        {
            content = content[..wordBoundary].TrimEnd();
        }

        return $"{content}{ellipsis}";
    }

    [GeneratedRegex(
        """^[a-z0-9][a-z0-9-]{0,39}$""",
        RegexOptions.CultureInvariant)]
    private static partial Regex NodeIdRegex();
}
