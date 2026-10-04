using AzHST.Application.Abstractions;
using AzHST.Application.Exceptions;
using AzHST.Application.Models;
using AzHST.Application.Services;

namespace AzHST.Application.Tests;

public sealed class GeneratePresentationUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ValidatesPlanAndBuildsPresentation()
    {
        var calls = new List<string>();
        var planner = new StubPlanner(CreateValidPlan(), calls);
        var builder = new StubBuilder(calls);
        var useCase = new GeneratePresentationUseCase(
            planner,
            builder,
            new StubAzureIconCatalog("networking/app-gateway"),
            new StubOutputThemeCatalog());
        var visualization = CreateVisualization();

        var result = await useCase.ExecuteAsync(
            "  Explain Azure Application Gateway  ",
            visualization,
            new AppSettings());

        Assert.Equal(["plan", "build"], calls);
        Assert.Equal("Explain Azure Application Gateway", planner.Query);
        Assert.Equal(CopilotModelSelection.Automatic, planner.Model);
        Assert.Same(visualization, planner.Visualization);
        Assert.Same(visualization, builder.Visualization);
        Assert.Equal(
            OutputThemeSettings.DefaultPresentationThemeId,
            builder.Theme?.Id);
        Assert.Equal("diagram", builder.Plan!.Slides[0].Kind);
        Assert.Equal(
            "How requests are processed",
            builder.Plan.Slides[0].SectionTitle);
        Assert.Equal(
            "Listener accepts HTTPS",
            builder.Plan.Slides[0].Subtitle);
        Assert.Equal(
            "Important: frontend and backend TLS are separate.",
            builder.Plan.Slides[0].Callout);
        var source = Assert.Single(builder.Plan.Sources);
        Assert.Equal("Application Gateway documentation", source.Title);
        Assert.Equal(
            "https://learn.microsoft.com/azure/application-gateway/",
            source.Url);
        Assert.Equal("cards", builder.Plan.Slides[1].Kind);
        Assert.Equal(builder.Artifact, result);
    }

    [Fact]
    public async Task ExecuteAsync_UsesSelectedModel()
    {
        var planner = new StubPlanner(CreateValidPlan(), []);
        var useCase = new GeneratePresentationUseCase(
            planner,
            new StubBuilder([]),
            new StubAzureIconCatalog("networking/app-gateway"),
            new StubOutputThemeCatalog());

        await useCase.ExecuteAsync(
            "Explain Azure Application Gateway",
            CreateVisualization(),
            new AppSettings
            {
                Model = "gpt-5",
            });

        Assert.Equal("gpt-5", planner.Model);
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesPlannerFailureWithoutBuilding()
    {
        var expected = new PresentationGenerationException(
            "The GitHub Copilot SDK could not plan the presentation.",
            new InvalidOperationException("The selected model is unavailable."));
        var builder = new StubBuilder([]);
        var useCase = new GeneratePresentationUseCase(
            new ThrowingPlanner(expected),
            builder,
            new StubAzureIconCatalog("networking/app-gateway"),
            new StubOutputThemeCatalog());

        var actual = await Assert.ThrowsAsync<PresentationGenerationException>(
            () => useCase.ExecuteAsync(
                "Explain Azure Application Gateway",
                CreateVisualization(),
                new AppSettings()));

        Assert.Same(expected, actual);
        Assert.Null(builder.Plan);
    }

    [Fact]
    public async Task ExecuteAsync_RemovesSectionTitleThatDuplicatesSlideTitle()
    {
        var plan = CreateValidPlan();
        plan.Slides[0].SectionTitle = "REQUEST FLOW";
        var builder = new StubBuilder([]);
        var useCase = new GeneratePresentationUseCase(
            new StubPlanner(plan, []),
            builder,
            new StubAzureIconCatalog("networking/app-gateway"),
            new StubOutputThemeCatalog());

        await useCase.ExecuteAsync(
            "Explain Azure Application Gateway",
            CreateVisualization(),
            new AppSettings());

        Assert.Equal(string.Empty, builder.Plan!.Slides[0].SectionTitle);
    }

    [Fact]
    public async Task ExecuteAsync_AcceptsNodeDetailAtMaximumLength()
    {
        var plan = CreateValidPlan();
        var detail = new string('x', 220);
        plan.Slides[1].Nodes[0].Detail = detail;
        var builder = new StubBuilder([]);
        var useCase = new GeneratePresentationUseCase(
            new StubPlanner(plan, []),
            builder,
            new StubAzureIconCatalog("networking/app-gateway"),
            new StubOutputThemeCatalog());

        await useCase.ExecuteAsync(
            "Explain Azure Application Gateway",
            CreateVisualization(),
            new AppSettings());

        Assert.Equal(detail, builder.Plan!.Slides[1].Nodes[0].Detail);
    }

    [Fact]
    public async Task ExecuteAsync_TruncatesCalloutAboveMaximumLength()
    {
        var plan = CreateValidPlan();
        plan.Slides[0].Callout = new string(
            'x',
            GeneratePresentationUseCase.MaximumCalloutLength + 20);
        var builder = new StubBuilder([]);
        var useCase = new GeneratePresentationUseCase(
            new StubPlanner(plan, []),
            builder,
            new StubAzureIconCatalog("networking/app-gateway"),
            new StubOutputThemeCatalog());

        await useCase.ExecuteAsync(
            "Explain Azure Application Gateway",
            CreateVisualization(),
            new AppSettings());

        Assert.Equal(
            GeneratePresentationUseCase.MaximumCalloutLength,
            builder.Plan!.Slides[0].Callout.Length);
        Assert.EndsWith("…", builder.Plan.Slides[0].Callout);
    }

    [Fact]
    public async Task ExecuteAsync_TruncatesNodeDetailAboveMaximumLength()
    {
        var plan = CreateValidPlan();
        plan.Slides[1].Nodes[0].Detail = new string(
            'x',
            GeneratePresentationUseCase.MaximumNodeDetailLength + 20);
        var builder = new StubBuilder([]);
        var useCase = new GeneratePresentationUseCase(
            new StubPlanner(plan, []),
            builder,
            new StubAzureIconCatalog("networking/app-gateway"),
            new StubOutputThemeCatalog());

        await useCase.ExecuteAsync(
            "Explain Azure Application Gateway",
            CreateVisualization(),
            new AppSettings());

        var detail = builder.Plan!.Slides[1].Nodes[0].Detail;
        Assert.Equal(
            GeneratePresentationUseCase.MaximumNodeDetailLength,
            detail.Length);
        Assert.EndsWith("…", detail);
    }

    [Fact]
    public async Task ExecuteAsync_TruncatesBulletAboveMaximumLength()
    {
        var plan = CreateValidPlan();
        plan.Slides[^1].Bullets[0] = new string(
            'x',
            GeneratePresentationUseCase.MaximumBulletLength + 20);
        var builder = new StubBuilder([]);
        var useCase = new GeneratePresentationUseCase(
            new StubPlanner(plan, []),
            builder,
            new StubAzureIconCatalog("networking/app-gateway"),
            new StubOutputThemeCatalog());

        await useCase.ExecuteAsync(
            "Explain Azure Application Gateway",
            CreateVisualization(),
            new AppSettings());

        var bullet = builder.Plan!.Slides[^1].Bullets[0];
        Assert.Equal(
            GeneratePresentationUseCase.MaximumBulletLength,
            bullet.Length);
        Assert.EndsWith("…", bullet);
    }

    [Fact]
    public async Task ExecuteAsync_TruncatesEveryOverlongDisplayTextField()
    {
        var plan = CreateValidPlan();
        var sourceTitle = new string(
            'r',
            PresentationSourcePolicy.MaximumTitleLength + 20);
        plan.Title = new string(
            'p',
            GeneratePresentationUseCase.MaximumTitleLength + 20);
        plan.Subtitle = new string(
            'q',
            GeneratePresentationUseCase.MaximumSubtitleLength + 20);
        plan.Sources[0].Title = sourceTitle;

        var slide = plan.Slides[0];
        slide.SectionTitle = new string(
            's',
            GeneratePresentationUseCase.MaximumSectionTitleLength + 20);
        slide.Title = new string(
            't',
            GeneratePresentationUseCase.MaximumTitleLength + 20);
        slide.Subtitle = new string(
            'u',
            GeneratePresentationUseCase.MaximumSlideSubtitleLength + 20);
        slide.Summary = new string(
            'v',
            GeneratePresentationUseCase.MaximumSummaryLength + 20);
        slide.Callout = new string(
            'w',
            GeneratePresentationUseCase.MaximumCalloutLength + 20);
        slide.Sources = [sourceTitle];
        slide.Nodes[0].Label = new string(
            'x',
            GeneratePresentationUseCase.MaximumNodeLabelLength + 20);
        slide.Nodes[0].Detail = new string(
            'y',
            GeneratePresentationUseCase.MaximumNodeDetailLength + 20);
        slide.Connections[0].Label = new string(
            'z',
            GeneratePresentationUseCase.MaximumConnectionLabelLength + 20);

        var summarySlide = plan.Slides[^1];
        summarySlide.Bullets[0] = new string(
            'b',
            GeneratePresentationUseCase.MaximumBulletLength + 20);

        var builder = new StubBuilder([]);
        var useCase = new GeneratePresentationUseCase(
            new StubPlanner(plan, []),
            builder,
            new StubAzureIconCatalog("networking/app-gateway"),
            new StubOutputThemeCatalog());

        await useCase.ExecuteAsync(
            "Explain Azure Application Gateway",
            CreateVisualization(),
            new AppSettings());

        var normalized = builder.Plan!;
        AssertTruncated(
            normalized.Title,
            GeneratePresentationUseCase.MaximumTitleLength);
        AssertTruncated(
            normalized.Subtitle,
            GeneratePresentationUseCase.MaximumSubtitleLength);
        AssertTruncated(
            normalized.Sources[0].Title,
            PresentationSourcePolicy.MaximumTitleLength);

        var normalizedSlide = normalized.Slides[0];
        AssertTruncated(
            normalizedSlide.SectionTitle,
            GeneratePresentationUseCase.MaximumSectionTitleLength);
        AssertTruncated(
            normalizedSlide.Title,
            GeneratePresentationUseCase.MaximumTitleLength);
        AssertTruncated(
            normalizedSlide.Subtitle,
            GeneratePresentationUseCase.MaximumSlideSubtitleLength);
        AssertTruncated(
            normalizedSlide.Summary,
            GeneratePresentationUseCase.MaximumSummaryLength);
        AssertTruncated(
            normalizedSlide.Callout,
            GeneratePresentationUseCase.MaximumCalloutLength);
        AssertTruncated(
            normalizedSlide.Sources[0],
            PresentationSourcePolicy.MaximumTitleLength);
        AssertTruncated(
            normalizedSlide.Nodes[0].Label,
            GeneratePresentationUseCase.MaximumNodeLabelLength);
        AssertTruncated(
            normalizedSlide.Nodes[0].Detail,
            GeneratePresentationUseCase.MaximumNodeDetailLength);
        AssertTruncated(
            normalizedSlide.Connections[0].Label,
            GeneratePresentationUseCase.MaximumConnectionLabelLength);
        AssertTruncated(
            normalized.Slides[^1].Bullets[0],
            GeneratePresentationUseCase.MaximumBulletLength);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsUnapprovedSourceUrl()
    {
        var plan = CreateValidPlan();
        plan.Sources[0].Url = "https://example.com/application-gateway";
        var useCase = new GeneratePresentationUseCase(
            new StubPlanner(plan, []),
            new StubBuilder([]),
            new StubAzureIconCatalog("networking/app-gateway"),
            new StubOutputThemeCatalog());

        var exception = await Assert.ThrowsAsync<PresentationGenerationException>(
            () => useCase.ExecuteAsync(
                "Explain Azure Application Gateway",
                CreateVisualization(),
                new AppSettings()));

        Assert.Contains("approved Microsoft HTTPS URL", exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_AcceptsAllSourcesOnOneSlide()
    {
        var plan = CreateValidPlan();
        plan.Sources = Enumerable.Range(
                1,
                PresentationSourcePolicy.MaximumSourceCount)
            .Select(index => new PresentationSourcePlan
            {
                Title = $"Microsoft reference {index}",
                Url =
                    $"https://learn.microsoft.com/azure/reference-{index}",
            })
            .ToList();
        plan.Slides[0].Sources = plan.Sources
            .Select(source => source.Title)
            .ToList();
        var builder = new StubBuilder([]);
        var useCase = new GeneratePresentationUseCase(
            new StubPlanner(plan, []),
            builder,
            new StubAzureIconCatalog("networking/app-gateway"),
            new StubOutputThemeCatalog());

        await useCase.ExecuteAsync(
            "Explain Azure Application Gateway",
            CreateVisualization(),
            new AppSettings());

        Assert.Equal(
            PresentationSourcePolicy.MaximumSourceCount,
            builder.Plan!.Slides[0].Sources.Count);
        Assert.Equal(
            PresentationSourcePolicy.MaximumSourceCount,
            builder.Plan.Sources.Count);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesLegacySlideSourceNames()
    {
        var plan = CreateValidPlan();
        plan.Sources = [];
        plan.Slides[0].Sources = ["Microsoft Learn"];
        var builder = new StubBuilder([]);
        var useCase = new GeneratePresentationUseCase(
            new StubPlanner(plan, []),
            builder,
            new StubAzureIconCatalog("networking/app-gateway"),
            new StubOutputThemeCatalog());

        await useCase.ExecuteAsync(
            "Explain Azure Application Gateway",
            CreateVisualization(),
            new AppSettings());

        var source = Assert.Single(builder.Plan!.Sources);
        Assert.Equal("Microsoft Learn", source.Title);
        Assert.Equal(string.Empty, source.Url);
    }

    [Fact]
    public async Task ExecuteAsync_UsesSelectedPresentationTheme()
    {
        var planner = new StubPlanner(CreateValidPlan(), []);
        var builder = new StubBuilder([]);
        var useCase = new GeneratePresentationUseCase(
            planner,
            builder,
            new StubAzureIconCatalog("networking/app-gateway"),
            new StubOutputThemeCatalog("custom-light"));

        await useCase.ExecuteAsync(
            "Explain Azure Application Gateway",
            CreateVisualization(),
            new AppSettings
            {
                Themes = new OutputThemeSettings
                {
                    PresentationThemeId = "custom-light",
                },
            });

        Assert.Equal("custom-light", builder.Theme?.Id);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsPlanWhoseFirstSlideIsNotVisual()
    {
        var plan = CreateValidPlan();
        plan.Slides[0].Kind = "content";
        plan.Slides[0].Nodes = [];
        plan.Slides[0].Connections = [];
        plan.Slides[0].Bullets = ["A content-only opening slide."];
        var builder = new StubBuilder([]);
        var useCase = new GeneratePresentationUseCase(
            new StubPlanner(plan, []),
            builder,
            new StubAzureIconCatalog("networking/app-gateway"),
            new StubOutputThemeCatalog());

        var exception = await Assert.ThrowsAsync<PresentationGenerationException>(
            () => useCase.ExecuteAsync(
                "Explain Azure Application Gateway",
                CreateVisualization(),
                new AppSettings()));

        Assert.Contains("first presentation content slide must be visual", exception.Message);
        Assert.Null(builder.Plan);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsPlanWithoutEnoughVisualSlides()
    {
        var plan = CreateValidPlan();
        foreach (var slide in plan.Slides.Skip(1).Take(2))
        {
            slide.Kind = "content";
            slide.Nodes = [];
            slide.Connections = [];
            slide.Bullets = ["A text-only slide."];
        }

        var builder = new StubBuilder([]);
        var useCase = new GeneratePresentationUseCase(
            new StubPlanner(plan, []),
            builder,
            new StubAzureIconCatalog("networking/app-gateway"),
            new StubOutputThemeCatalog());

        var exception = await Assert.ThrowsAsync<PresentationGenerationException>(
            () => useCase.ExecuteAsync(
                "Explain Azure Application Gateway",
                CreateVisualization(),
                new AppSettings()));

        Assert.Contains("At least 3 of the 4 content slides must be visual", exception.Message);
        Assert.Null(builder.Plan);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsUnknownAzureIcon()
    {
        var plan = CreateValidPlan();
        plan.Slides[0].Nodes[1].IconKey = "networking/unknown";
        var builder = new StubBuilder([]);
        var useCase = new GeneratePresentationUseCase(
            new StubPlanner(plan, []),
            builder,
            new StubAzureIconCatalog("networking/app-gateway"),
            new StubOutputThemeCatalog());

        var exception = await Assert.ThrowsAsync<PresentationGenerationException>(
            () => useCase.ExecuteAsync(
                "Explain Azure Application Gateway",
                CreateVisualization(),
                new AppSettings()));

        Assert.Contains("unavailable Azure icon", exception.Message);
        Assert.Null(builder.Plan);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsConnectionToMissingNode()
    {
        var plan = CreateValidPlan();
        plan.Slides[0].Connections[0].To = "missing";
        var builder = new StubBuilder([]);
        var useCase = new GeneratePresentationUseCase(
            new StubPlanner(plan, []),
            builder,
            new StubAzureIconCatalog("networking/app-gateway"),
            new StubOutputThemeCatalog());

        var exception = await Assert.ThrowsAsync<PresentationGenerationException>(
            () => useCase.ExecuteAsync(
                "Explain Azure Application Gateway",
                CreateVisualization(),
                new AppSettings()));

        Assert.Contains("invalid connection", exception.Message);
        Assert.Null(builder.Plan);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsVisualSlideWithIgnoredBullets()
    {
        var plan = CreateValidPlan();
        plan.Slides[0].Bullets = ["This would not fit the visual layout."];
        var builder = new StubBuilder([]);
        var useCase = new GeneratePresentationUseCase(
            new StubPlanner(plan, []),
            builder,
            new StubAzureIconCatalog("networking/app-gateway"),
            new StubOutputThemeCatalog());

        var exception = await Assert.ThrowsAsync<PresentationGenerationException>(
            () => useCase.ExecuteAsync(
                "Explain Azure Application Gateway",
                CreateVisualization(),
                new AppSettings()));

        Assert.Contains("node details instead of bullet", exception.Message);
        Assert.Null(builder.Plan);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsOverloadedDiagram()
    {
        var plan = CreateValidPlan();
        for (var index = 0; index < 5; index++)
        {
            plan.Slides[0].Nodes.Add(new PresentationNodePlan
            {
                Id = $"extra-{index}",
                Label = $"Extra {index}",
                Detail = "A focused slide should explain this node.",
                Tone = "neutral",
            });
        }

        var builder = new StubBuilder([]);
        var useCase = new GeneratePresentationUseCase(
            new StubPlanner(plan, []),
            builder,
            new StubAzureIconCatalog("networking/app-gateway"),
            new StubOutputThemeCatalog());

        var exception = await Assert.ThrowsAsync<PresentationGenerationException>(
            () => useCase.ExecuteAsync(
                "Explain Azure Application Gateway",
                CreateVisualization(),
                new AppSettings()));

        Assert.Contains("no more than 6 nodes", exception.Message);
        Assert.Null(builder.Plan);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsUnsupportedNodeTone()
    {
        var plan = CreateValidPlan();
        plan.Slides[1].Nodes[0].Tone = "rainbow";
        var builder = new StubBuilder([]);
        var useCase = new GeneratePresentationUseCase(
            new StubPlanner(plan, []),
            builder,
            new StubAzureIconCatalog("networking/app-gateway"),
            new StubOutputThemeCatalog());

        var exception = await Assert.ThrowsAsync<PresentationGenerationException>(
            () => useCase.ExecuteAsync(
                "Explain Azure Application Gateway",
                CreateVisualization(),
                new AppSettings()));

        Assert.Contains("unsupported node tone", exception.Message);
        Assert.Null(builder.Plan);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsVisualizationWithoutHtml()
    {
        var visualization = CreateVisualization() with
        {
            Html = string.Empty,
        };
        var useCase = new GeneratePresentationUseCase(
            new StubPlanner(CreateValidPlan(), []),
            new StubBuilder([]),
            new StubAzureIconCatalog("networking/app-gateway"),
            new StubOutputThemeCatalog());

        var exception = await Assert.ThrowsAsync<PresentationGenerationException>(
            () => useCase.ExecuteAsync(
                "Explain Azure Application Gateway",
                visualization,
                new AppSettings()));

        Assert.Contains("HTML visualization is unavailable", exception.Message);
    }

    private static PresentationPlan CreateValidPlan()
    {
        return new PresentationPlan
        {
            Title = "Azure Application Gateway",
            Subtitle = "Request routing and protection",
            Sources =
            [
                new PresentationSourcePlan
                {
                    Title = "Application Gateway documentation",
                    Url =
                        "https://learn.microsoft.com/azure/application-gateway/",
                },
            ],
            Slides =
            [
                new PresentationSlidePlan
                {
                    Kind = "diagram",
                    SectionTitle = "How requests are processed",
                    Title = "Request flow",
                    Subtitle = "Listener accepts HTTPS",
                    Summary = "Traffic flows through the gateway.",
                    Callout =
                        "Important: frontend and backend TLS are separate.",
                    Nodes =
                    [
                        new PresentationNodePlan
                        {
                            Id = "client",
                            Label = "Client",
                            Detail = "Sends HTTPS.",
                            Tone = "neutral",
                        },
                        new PresentationNodePlan
                        {
                            Id = "gateway",
                            Label = "Application Gateway",
                            Detail = "Routes and protects.",
                            IconKey = "networking/app-gateway",
                            Tone = "primary",
                        },
                    ],
                    Connections =
                    [
                        new PresentationConnectionPlan
                        {
                            From = "client",
                            To = "gateway",
                            Label = "HTTPS",
                        },
                    ],
                },
                new PresentationSlidePlan
                {
                    Kind = "cards",
                    Title = "Core capabilities",
                    Summary = "The gateway combines routing, protection, and health-aware delivery.",
                    Nodes =
                    [
                        new PresentationNodePlan
                        {
                            Id = "routing",
                            Label = "Layer 7 routing",
                            Detail = "Matches listeners and rules to backend pools.",
                            Tone = "accent",
                        },
                        new PresentationNodePlan
                        {
                            Id = "waf",
                            Label = "Web protection",
                            Detail = "Applies managed and custom WAF policies.",
                            Tone = "success",
                        },
                        new PresentationNodePlan
                        {
                            Id = "health",
                            Label = "Backend health",
                            Detail = "Sends traffic only to healthy endpoints.",
                            Tone = "primary",
                        },
                    ],
                },
                new PresentationSlidePlan
                {
                    Kind = "comparison",
                    Title = "Deployment considerations",
                    Summary = "Choose the topology that matches exposure and operational needs.",
                    Nodes =
                    [
                        new PresentationNodePlan
                        {
                            Id = "public",
                            Label = "Public frontend",
                            Detail = "Accepts internet-facing application traffic.",
                            Tone = "primary",
                        },
                        new PresentationNodePlan
                        {
                            Id = "private",
                            Label = "Private frontend",
                            Detail = "Restricts access to private network paths.",
                            Tone = "neutral",
                        },
                    ],
                },
                new PresentationSlidePlan
                {
                    Kind = "summary",
                    Title = "Validate before production",
                    Summary = "Confirm current Azure details.",
                    Bullets = ["Review regional availability and pricing."],
                },
            ],
        };
    }

    private static VisualizationArtifact CreateVisualization()
    {
        return new VisualizationArtifact(
            "0199abcd1234-app-gateway",
            @"C:\generated\0199abcd1234-app-gateway",
            @"C:\generated\0199abcd1234-app-gateway\index.html",
            new Uri("file:///C:/generated/0199abcd1234-app-gateway/index.html"))
        {
            Html = """
                <!doctype html>
                <html>
                <head><title>Azure Application Gateway</title></head>
                <body>
                  <main>
                    <h1>Azure Application Gateway</h1>
                    <section>
                      <h2>Request flow</h2>
                      <p>Client traffic passes through WAF and routing rules.</p>
                    </section>
                  </main>
                </body>
                </html>
                """,
        };
    }

    private static void AssertTruncated(string value, int maximumLength)
    {
        Assert.Equal(maximumLength, value.Length);
        Assert.EndsWith("…", value);
    }

    private sealed class StubPlanner(
        PresentationPlan plan,
        List<string> calls) : ICopilotPresentationPlanner
    {
        public string? Query { get; private set; }

        public string? Model { get; private set; }

        public VisualizationArtifact? Visualization { get; private set; }

        public Task<PresentationPlan> CreatePresentationPlanAsync(
            string query,
            string model,
            VisualizationArtifact visualization,
            IProgress<GenerationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            calls.Add("plan");
            Query = query;
            Model = model;
            Visualization = visualization;
            return Task.FromResult(plan);
        }
    }

    private sealed class ThrowingPlanner(
        PresentationGenerationException exception)
        : ICopilotPresentationPlanner
    {
        public Task<PresentationPlan> CreatePresentationPlanAsync(
            string query,
            string model,
            VisualizationArtifact visualization,
            IProgress<GenerationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            progress?.Report(new GenerationProgress(
                GenerationStage.PlanningPresentation,
                "Planning PowerPoint slides..."));
            return Task.FromException<PresentationPlan>(exception);
        }
    }

    private sealed class StubBuilder(List<string> calls) : IPresentationBuilder
    {
        public PresentationPlan? Plan { get; private set; }

        public VisualizationArtifact? Visualization { get; private set; }

        public PresentationThemeDefinition? Theme { get; private set; }

        public PresentationArtifact Artifact { get; } = new(
            "0199abcd1234-app-gateway",
            @"C:\generated\0199abcd1234-app-gateway\presentation.pptx",
            new Uri("file:///C:/generated/0199abcd1234-app-gateway/presentation.pptx"),
            5);

        public Task<PresentationArtifact> BuildAsync(
            PresentationPlan plan,
            VisualizationArtifact visualization,
            PresentationThemeDefinition theme,
            CancellationToken cancellationToken = default)
        {
            calls.Add("build");
            Plan = plan;
            Visualization = visualization;
            Theme = theme;
            return Task.FromResult(Artifact);
        }
    }

    private sealed class StubOutputThemeCatalog : IOutputThemeCatalog
    {
        private readonly PresentationThemeDefinition _presentationTheme;

        public StubOutputThemeCatalog(
            string presentationThemeId =
                OutputThemeSettings.DefaultPresentationThemeId)
        {
            _presentationTheme = new PresentationThemeDefinition
            {
                SchemaVersion = 1,
                Id = presentationThemeId,
                DisplayName = "Presentation theme",
            };
            PresentationThemes =
            [
                new(presentationThemeId, "Presentation theme"),
            ];
        }

        public IReadOnlyList<OutputThemeOption> HtmlThemes { get; } =
        [
            new(OutputThemeSettings.DefaultHtmlThemeId, "Azure Night"),
        ];

        public IReadOnlyList<OutputThemeOption> PresentationThemes { get; }

        public HtmlThemeDefinition GetHtmlTheme(string id)
        {
            throw new NotSupportedException();
        }

        public PresentationThemeDefinition GetPresentationTheme(string id)
        {
            Assert.Equal(_presentationTheme.Id, id);
            return _presentationTheme;
        }
    }

    private sealed class StubAzureIconCatalog(
        string availableKey) : IAzureIconCatalog
    {
        public IReadOnlyList<AzureIconDescriptor> FindRelevant(
            string query,
            int maximumResults)
        {
            return [];
        }

        public bool TryGetDataUri(string key, out string dataUri)
        {
            if (string.Equals(key, availableKey, StringComparison.Ordinal))
            {
                dataUri = "data:image/svg+xml;base64,PHN2Zy8+";
                return true;
            }

            dataUri = string.Empty;
            return false;
        }
    }
}
