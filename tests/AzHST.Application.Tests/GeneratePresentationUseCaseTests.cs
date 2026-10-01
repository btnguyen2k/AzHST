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
            new AppSettings { Model = "  gpt-5  " });

        Assert.Equal(["plan", "build"], calls);
        Assert.Equal("Explain Azure Application Gateway", planner.Query);
        Assert.Equal("gpt-5", planner.Model);
        Assert.Equal(visualization.Id, planner.VisualizationId);
        Assert.Same(visualization, builder.Visualization);
        Assert.Equal(
            OutputThemeSettings.DefaultPresentationThemeId,
            builder.Theme?.Id);
        Assert.Equal("diagram", builder.Plan!.Slides[1].Kind);
        Assert.Equal(builder.Artifact, result);
    }

    [Fact]
    public async Task ExecuteAsync_UsesDefaultModelWhenSettingIsBlank()
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
            new AppSettings { Model = " " });

        Assert.Equal(AppSettings.DefaultModel, planner.Model);
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
    public async Task ExecuteAsync_RejectsPlanWithoutVisualSlide()
    {
        var plan = CreateValidPlan();
        plan.Slides[1].Kind = "content";
        plan.Slides[1].Nodes = [];
        plan.Slides[1].Connections = [];
        plan.Slides[1].Bullets = ["A content-only slide."];
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

        Assert.Contains("visual diagram or comparison", exception.Message);
        Assert.Null(builder.Plan);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsUnknownAzureIcon()
    {
        var plan = CreateValidPlan();
        plan.Slides[1].Nodes[1].IconKey = "networking/unknown";
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
        plan.Slides[1].Connections[0].To = "missing";
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
        plan.Slides[1].Bullets = ["This would not fit the visual layout."];
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

    private static PresentationPlan CreateValidPlan()
    {
        return new PresentationPlan
        {
            Title = "Azure Application Gateway",
            Subtitle = "Request routing and protection",
            Slides =
            [
                new PresentationSlidePlan
                {
                    Kind = "content",
                    Title = "Overview",
                    Summary = "Application Gateway is a layer 7 load balancer.",
                    Bullets = ["Routes requests using listeners and rules."],
                },
                new PresentationSlidePlan
                {
                    Kind = "diagram",
                    Title = "Request flow",
                    Summary = "Traffic flows through the gateway.",
                    Nodes =
                    [
                        new PresentationNodePlan
                        {
                            Id = "client",
                            Label = "Client",
                            Detail = "Sends HTTPS.",
                        },
                        new PresentationNodePlan
                        {
                            Id = "gateway",
                            Label = "Application Gateway",
                            Detail = "Routes and protects.",
                            IconKey = "networking/app-gateway",
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
            new Uri("file:///C:/generated/0199abcd1234-app-gateway/index.html"));
    }

    private sealed class StubPlanner(
        PresentationPlan plan,
        List<string> calls) : ICopilotPresentationPlanner
    {
        public string? Query { get; private set; }

        public string? Model { get; private set; }

        public string? VisualizationId { get; private set; }

        public Task<PresentationPlan> CreatePresentationPlanAsync(
            string query,
            string model,
            string visualizationId,
            IProgress<GenerationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            calls.Add("plan");
            Query = query;
            Model = model;
            VisualizationId = visualizationId;
            return Task.FromResult(plan);
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
            4);

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
