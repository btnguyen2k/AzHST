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
          </main>
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
                Model = "  gpt-5  ",
                OutputDirectory = "generated",
            });

        var expectedId =
            $"{FixedTime.ToUnixTimeMilliseconds():x12}-azure-functions";

        Assert.Equal(["assess", "generate"], client.CallOrder);
        Assert.Equal("Explain Azure Functions", client.AssessmentQuery);
        Assert.Equal("Explain Azure Functions", client.GenerationQuery);
        Assert.Equal("gpt-5", client.AssessmentModel);
        Assert.Equal("gpt-5", client.GenerationModel);
        Assert.Equal(expectedId, client.VisualizationId);
        Assert.Equal(expectedId, store.VisualizationId);
        Assert.Equal("generated", store.OutputDirectory);
        Assert.Contains("Content-Security-Policy", store.Html);
        Assert.Equal(expectedId, result.Id);
        Assert.Equal(store.Artifact, result);
    }

    [Fact]
    public async Task ExecuteAsync_UsesAutoModelWhenSettingIsBlank()
    {
        var client = CreateValidClient();
        var useCase = CreateUseCase(client, new StubArtifactStore());

        await useCase.ExecuteAsync("Compare two services", new AppSettings { Model = " " });

        Assert.Equal(AppSettings.DefaultModel, client.AssessmentModel);
        Assert.Equal(AppSettings.DefaultModel, client.GenerationModel);
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
        StubArtifactStore store)
    {
        return new GenerateVisualizationUseCase(
            client,
            new GeneratedHtmlDocumentProcessor(),
            store,
            new VisualizationArtifactIdGenerator(new FixedTimeProvider(FixedTime)));
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
            IProgress<GenerationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            CallOrder.Add("generate");
            GenerationQuery = query;
            GenerationModel = model;
            VisualizationId = visualizationId;
            return Task.FromResult(response);
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
                new Uri($"file:///tmp/{visualizationId}/index.html"));
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
}
