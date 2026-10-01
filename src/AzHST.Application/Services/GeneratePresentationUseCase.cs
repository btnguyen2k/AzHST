using System.Text.RegularExpressions;
using AzHST.Application.Abstractions;
using AzHST.Application.Exceptions;
using AzHST.Application.Models;

namespace AzHST.Application.Services;

public sealed partial class GeneratePresentationUseCase
{
    public const int MinimumSlideCount = 3;
    public const int MaximumSlideCount = 10;
    public const int MaximumBulletsPerSlide = 6;
    public const int MaximumNodesPerSlide = 10;
    public const int MaximumConnectionsPerSlide = 16;

    private const int MaximumTitleLength = 120;
    private const int MaximumSubtitleLength = 240;
    private const int MaximumSummaryLength = 420;
    private const int MaximumBulletLength = 240;
    private const int MaximumNodeLabelLength = 80;
    private const int MaximumNodeDetailLength = 160;
    private const int MaximumConnectionLabelLength = 60;
    private const int MaximumSourcesPerSlide = 4;
    private const int MaximumSourceLength = 100;

    private static readonly HashSet<string> SupportedKinds = new(
        ["content", "diagram", "comparison", "summary"],
        StringComparer.Ordinal);

    private readonly ICopilotPresentationPlanner _planner;
    private readonly IPresentationBuilder _builder;
    private readonly IAzureIconCatalog _azureIcons;

    public GeneratePresentationUseCase(
        ICopilotPresentationPlanner planner,
        IPresentationBuilder builder,
        IAzureIconCatalog azureIcons)
    {
        _planner = planner;
        _builder = builder;
        _azureIcons = azureIcons;
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

        if (normalizedQuery.Length > GenerateVisualizationUseCase.MaximumQueryLength)
        {
            throw new PresentationGenerationException(
                $"The question exceeds {GenerateVisualizationUseCase.MaximumQueryLength:N0} characters.");
        }

        if (string.IsNullOrWhiteSpace(visualization.Id)
            || string.IsNullOrWhiteSpace(visualization.DirectoryPath))
        {
            throw new PresentationGenerationException(
                "The visualization artifact is missing its ID or output directory.");
        }

        var model = string.IsNullOrWhiteSpace(settings.Model)
            ? AppSettings.DefaultModel
            : settings.Model.Trim();

        var rawPlan = await _planner.CreatePresentationPlanAsync(
            normalizedQuery,
            model,
            visualization.Id,
            progress,
            cancellationToken);
        var plan = ValidateAndNormalize(rawPlan);

        progress?.Report(new GenerationProgress(
            GenerationStage.BuildingPresentation,
            "Building the PowerPoint presentation..."));

        var artifact = await _builder.BuildAsync(
            plan,
            visualization,
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
        var subtitle = OptionalText(plan.Subtitle, "presentation subtitle", MaximumSubtitleLength);
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

        if (!normalizedSlides.Any(
                slide => slide.Kind is "diagram" or "comparison"))
        {
            throw new PresentationGenerationException(
                "The presentation plan must include at least one visual diagram or comparison slide.");
        }

        return new PresentationPlan
        {
            Title = title,
            Subtitle = subtitle,
            Slides = normalizedSlides,
        };
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
        var summary = OptionalText(
            slide.Summary,
            $"slide {slideNumber} summary",
            MaximumSummaryLength);
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

        if (kind is "diagram" or "comparison")
        {
            if (nodes.Count < 2)
            {
                throw new PresentationGenerationException(
                    $"Visual slide {slideNumber} must contain at least two nodes.");
            }

            if (bullets.Count > 0)
            {
                throw new PresentationGenerationException(
                    $"Visual slide {slideNumber} must use node details instead of bullet points.");
            }

            if (kind == "comparison" && connections.Count > 0)
            {
                throw new PresentationGenerationException(
                    $"Comparison slide {slideNumber} must not contain connections.");
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
            Title = title,
            Summary = summary,
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

            normalized.Add(new PresentationNodePlan
            {
                Id = id,
                Label = RequireText(
                    node.Label,
                    $"slide {slideNumber} node label",
                    MaximumNodeLabelLength),
                Detail = OptionalText(
                    node.Detail,
                    $"slide {slideNumber} node detail",
                    MaximumNodeDetailLength),
                IconKey = iconKey,
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
                    $"slide {slideNumber} connection label",
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
            throw new PresentationGenerationException(
                $"The {fieldName} exceeds {maximumLength} characters.");
        }

        return normalized;
    }

    private static string OptionalText(
        string? value,
        string fieldName,
        int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length > maximumLength)
        {
            throw new PresentationGenerationException(
                $"The {fieldName} exceeds {maximumLength} characters.");
        }

        return normalized;
    }

    [GeneratedRegex(
        """^[a-z0-9][a-z0-9-]{0,39}$""",
        RegexOptions.CultureInvariant)]
    private static partial Regex NodeIdRegex();
}
