using AzHST.Application.Abstractions;
using AzHST.Application.Exceptions;
using AzHST.Application.Models;
using AzHST.Application.Services;

namespace AzHST.Application.Tests;

public sealed class GenerateVisualizationUseCaseTests
{
    private static readonly DateTimeOffset FixedTime =
        new(2026, 9, 30, 12, 34, 56, TimeSpan.Zero);

    private const string ValidHtml = """
        <!doctype html>
        <html>
        <head>
          <title>Azure Functions</title>
          <style>
            .active { transition: transform 200ms ease; }
            @media (prefers-reduced-motion: reduce) {
              .active { transition: none; }
            }
          </style>
        </head>
        <body>
          <main>
            <button id="next" type="button">Next step</button>
            <div id="stage">Visualization</div>
            <section id="sources">
              <h2>Sources</h2>
              <a data-azh-source href="https://learn.microsoft.com/azure/azure-functions/" target="_blank" rel="noopener noreferrer">Azure Functions documentation</a>
            </section>
          </main>
          <script id="azh-presentation-plan" type="application/json">
          {
            "title": "Azure Functions",
            "subtitle": "Safe visualization",
            "sources": [
              {
                "title": "Azure Functions documentation",
                "url": "https://learn.microsoft.com/azure/azure-functions/"
              }
            ],
            "slides": [
              {
                "kind": "diagram",
                "title": "Request flow",
                "summary": "Follow the request.",
                "bullets": [],
                "nodes": [
                  { "id": "source", "label": "Source", "detail": "Starts work.", "iconKey": "", "tone": "neutral" },
                  { "id": "function", "label": "Function", "detail": "Processes work.", "iconKey": "", "tone": "primary" }
                ],
                "connections": [
                  { "from": "source", "to": "function", "label": "invoke" }
                ],
                "sources": []
              },
              {
                "kind": "cards",
                "title": "Behavior",
                "summary": "Visible behavior.",
                "bullets": [],
                "nodes": [
                  { "id": "trigger", "label": "Trigger", "detail": "Starts execution.", "iconKey": "", "tone": "primary" },
                  { "id": "binding", "label": "Binding", "detail": "Connects data.", "iconKey": "", "tone": "accent" }
                ],
                "connections": [],
                "sources": []
              },
              {
                "kind": "cards",
                "title": "Operations",
                "summary": "Operational guidance.",
                "bullets": [],
                "nodes": [
                  { "id": "monitor", "label": "Monitor", "detail": "Observe executions.", "iconKey": "", "tone": "success" },
                  { "id": "retry", "label": "Retry", "detail": "Handle failures.", "iconKey": "", "tone": "warning" }
                ],
                "connections": [],
                "sources": []
              },
              {
                "kind": "cards",
                "title": "Validate before production",
                "summary": "Confirm current details.",
                "bullets": [],
                "nodes": [
                  { "id": "availability", "label": "Availability", "detail": "Confirm regional support.", "iconKey": "", "tone": "warning" },
                  { "id": "pricing", "label": "Pricing", "detail": "Review current pricing.", "iconKey": "", "tone": "warning" }
                ],
                "connections": [],
                "sources": ["Microsoft Learn"]
              }
            ]
          }
          </script>
          <script>
            document.querySelector("#next").addEventListener("click", () => {
              document.querySelector("#stage").classList.toggle("active");
            });
          </script>
        </body>
        </html>
        """;

    [Fact]
    public async Task ExecuteAsync_TrimsInputSecuresHtmlAndSavesArtifact()
    {
        var client = new StubCopilotClient(
            new VisualizationQueryAssessment
            {
                IsValid = true,
                Message = "Valid Azure visualization request.",
                SuggestedSlug = "azure-functions",
            },
            ValidHtml);
        var store = new StubArtifactStore();
        var useCase = CreateUseCase(client, store);

        var result = await useCase.ExecuteAsync(
            "  Explain Azure Functions  ",
            new AppSettings
            {
                OutputDirectory = "generated",
            });

        var expectedId =
            $"{FixedTime.ToUnixTimeMilliseconds():x12}-azure-functions";

        Assert.Equal(["assess", "generate"], client.CallOrder);
        Assert.Equal("Explain Azure Functions", client.AssessmentQuery);
        Assert.Equal("Explain Azure Functions", client.GenerationQuery);
        Assert.Equal(CopilotModelSelection.Automatic, client.AssessmentModel);
        Assert.Equal(CopilotModelSelection.Automatic, client.GenerationModel);
        Assert.Equal(expectedId, client.VisualizationId);
        Assert.Equal(
            OutputThemeSettings.DefaultHtmlThemeId,
            client.HtmlTheme?.Id);
        Assert.Equal(expectedId, store.VisualizationId);
        Assert.Equal("generated", store.OutputDirectory);
        Assert.Contains("Content-Security-Policy", store.Html);
        Assert.Equal(expectedId, result.Id);
        Assert.Equal(store.Artifact, result);
    }

    [Fact]
    public async Task ExecuteAsync_UsesSelectedModel()
    {
        var client = CreateValidClient();
        var useCase = CreateUseCase(client, new StubArtifactStore());

        await useCase.ExecuteAsync(
            "Compare two services",
            new AppSettings
            {
                Model = "claude-sonnet-4.5",
            });

        Assert.Equal("claude-sonnet-4.5", client.AssessmentModel);
        Assert.Equal("claude-sonnet-4.5", client.GenerationModel);
    }

    [Fact]
    public async Task ExecuteAsync_UsesSelectedHtmlTheme()
    {
        var client = CreateValidClient();
        var themes = new StubOutputThemeCatalog("custom-night");
        var useCase = CreateUseCase(
            client,
            new StubArtifactStore(),
            themes);

        await useCase.ExecuteAsync(
            "Explain Azure Functions",
            new AppSettings
            {
                Themes = new OutputThemeSettings
                {
                    HtmlThemeId = "custom-night",
                },
            });

        Assert.Equal("custom-night", client.HtmlTheme?.Id);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsInvalidAssessedQueryWithoutGeneratingHtml()
    {
        var client = new StubCopilotClient(
            new VisualizationQueryAssessment
            {
                IsValid = false,
                Message = "Ask about an Azure or Microsoft cloud topic that can be visualized.",
            },
            ValidHtml);
        var store = new StubArtifactStore();
        var useCase = CreateUseCase(client, store);

        var exception = await Assert.ThrowsAsync<InvalidVisualizationQueryException>(
            () => useCase.ExecuteAsync("Write a birthday poem", new AppSettings()));

        Assert.Equal(
            "Ask about an Azure or Microsoft cloud topic that can be visualized.",
            exception.Message);
        Assert.Equal(["assess"], client.CallOrder);
        Assert.False(store.SaveCalled);
    }

    [Fact]
    public async Task ExecuteAsync_UsesHelpfulFallbackForEmptyInvalidMessage()
    {
        var client = new StubCopilotClient(
            new VisualizationQueryAssessment { IsValid = false },
            ValidHtml);
        var useCase = CreateUseCase(client, new StubArtifactStore());

        var exception = await Assert.ThrowsAsync<InvalidVisualizationQueryException>(
            () => useCase.ExecuteAsync("Tell me a joke", new AppSettings()));

        Assert.Contains("Azure or Microsoft services", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_RejectsBlankQuery(string query)
    {
        var client = CreateValidClient();
        var useCase = CreateUseCase(client, new StubArtifactStore());

        var exception = await Assert.ThrowsAsync<VisualizationGenerationException>(
            () => useCase.ExecuteAsync(query, new AppSettings()));

        Assert.Contains("Enter", exception.Message);
        Assert.Empty(client.CallOrder);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsOversizedQuery()
    {
        var client = CreateValidClient();
        var useCase = CreateUseCase(client, new StubArtifactStore());

        var query = new string('x', GenerateVisualizationUseCase.MaximumQueryLength + 1);

        await Assert.ThrowsAsync<VisualizationGenerationException>(
            () => useCase.ExecuteAsync(query, new AppSettings()));
        Assert.Empty(client.CallOrder);
    }

    private static GenerateVisualizationUseCase CreateUseCase(
        StubCopilotClient client,
        StubArtifactStore store,
        IOutputThemeCatalog? themes = null)
    {
        return new GenerateVisualizationUseCase(
            client,
            new GeneratedHtmlDocumentProcessor(),
            store,
            new VisualizationArtifactIdGenerator(new FixedTimeProvider(FixedTime)),
            themes ?? new StubOutputThemeCatalog());
    }

    private static StubCopilotClient CreateValidClient()
    {
        return new StubCopilotClient(
            new VisualizationQueryAssessment
            {
                IsValid = true,
                Message = "Valid.",
                SuggestedSlug = "azure-visualization",
            },
            ValidHtml);
    }

    private sealed class StubCopilotClient(
        VisualizationQueryAssessment assessment,
        string response) : ICopilotVisualizationClient
    {
        public List<string> CallOrder { get; } = [];

        public string? AssessmentQuery { get; private set; }

        public string? AssessmentModel { get; private set; }

        public string? GenerationQuery { get; private set; }

        public string? GenerationModel { get; private set; }

        public string? VisualizationId { get; private set; }

        public HtmlThemeDefinition? HtmlTheme { get; private set; }

        public Task<VisualizationQueryAssessment> AssessQueryAsync(
            string query,
            string model,
            IProgress<GenerationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            CallOrder.Add("assess");
            AssessmentQuery = query;
            AssessmentModel = model;
            return Task.FromResult(assessment);
        }

        public Task<string> GenerateHtmlAsync(
            string query,
            string model,
            string visualizationId,
            HtmlThemeDefinition theme,
            IProgress<GenerationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            CallOrder.Add("generate");
            GenerationQuery = query;
            GenerationModel = model;
            VisualizationId = visualizationId;
            HtmlTheme = theme;
            return Task.FromResult(response);
        }
    }

    private sealed class StubOutputThemeCatalog : IOutputThemeCatalog
    {
        private readonly HtmlThemeDefinition _htmlTheme;
        private readonly PresentationThemeDefinition _presentationTheme =
            new()
            {
                SchemaVersion = 1,
                Id = OutputThemeSettings.DefaultPresentationThemeId,
                DisplayName = "Professional Light",
            };

        public StubOutputThemeCatalog(
            string htmlThemeId = OutputThemeSettings.DefaultHtmlThemeId)
        {
            _htmlTheme = CreateHtmlTheme(htmlThemeId);
            HtmlThemes =
            [
                new(htmlThemeId, "HTML theme"),
            ];
        }

        public IReadOnlyList<OutputThemeOption> HtmlThemes { get; }

        public IReadOnlyList<OutputThemeOption> PresentationThemes { get; } =
        [
            new(
                OutputThemeSettings.DefaultPresentationThemeId,
                "Professional Light"),
        ];

        public HtmlThemeDefinition GetHtmlTheme(string id)
        {
            Assert.Equal(_htmlTheme.Id, id);
            return _htmlTheme;
        }

        public PresentationThemeDefinition GetPresentationTheme(string id)
        {
            Assert.Equal(_presentationTheme.Id, id);
            return _presentationTheme;
        }
    }

    private sealed class StubArtifactStore : IGeneratedArtifactStore
    {
        public VisualizationArtifact? Artifact { get; private set; }

        public bool SaveCalled { get; private set; }

        public string? VisualizationId { get; private set; }

        public string? Html { get; private set; }

        public string? OutputDirectory { get; private set; }

        public Task<VisualizationArtifact> SaveAsync(
            string visualizationId,
            string html,
            string outputDirectory,
            CancellationToken cancellationToken = default)
        {
            SaveCalled = true;
            VisualizationId = visualizationId;
            Html = html;
            OutputDirectory = outputDirectory;
            Artifact = new VisualizationArtifact(
                visualizationId,
                $"/tmp/{visualizationId}",
                $"/tmp/{visualizationId}/index.html",
                new Uri($"file:///tmp/{visualizationId}/index.html"))
            {
                Html = html,
            };
            return Task.FromResult(Artifact);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }

    private static HtmlThemeDefinition CreateHtmlTheme(
        string id = OutputThemeSettings.DefaultHtmlThemeId)
    {
        return new HtmlThemeDefinition
        {
            SchemaVersion = 1,
            Id = id,
            DisplayName = "Azure Night",
            Palette = new HtmlThemePalette
            {
                Page = "#071426",
                Surface = "#0D1B2A",
                SurfaceRaised = "#12263D",
                SurfaceSelected = "#173554",
                Border = "#29415F",
                Text = "#F3F7FC",
                TextMuted = "#A9B8CC",
                Primary = "#4CA6FF",
                ActiveFlow = "#22D3EE",
                Accent = "#7DD3FC",
                Success = "#34D399",
                Warning = "#FBBF24",
                Danger = "#FB7185",
                Focus = "#93C5FD",
                IconTile = "#EAF4FC",
            },
            Typography = new HtmlThemeTypography
            {
                FontFamily = "Inter, Segoe UI, sans-serif",
                BaseSizePixels = 16,
                LineHeight = 1.5,
            },
            Appearance = new HtmlThemeAppearance
            {
                ColorScheme = HtmlColorScheme.Dark,
                SurfaceStyle = HtmlSurfaceStyle.Layered,
                CornerRadiusPixels = 14,
                IconTileSizePixels = 44,
                AllowPureBlack = false,
                AllowGlassmorphism = false,
            },
            Motion = new HtmlThemeMotion
            {
                TransitionDurationMilliseconds = 220,
                SequenceStepDurationMilliseconds = 650,
                AllowContinuousDecorativeMotion = false,
            },
        };
    }
}
