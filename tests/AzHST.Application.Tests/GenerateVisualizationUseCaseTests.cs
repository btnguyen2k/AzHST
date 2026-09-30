using AzHST.Application.Abstractions;
using AzHST.Application.Exceptions;
using AzHST.Application.Models;
using AzHST.Application.Services;

namespace AzHST.Application.Tests;

public sealed class GenerateVisualizationUseCaseTests
{
    private const string ValidHtml = """
        <!doctype html>
        <html>
        <head><title>Azure Functions</title></head>
        <body><main>Visualization</main></body>
        </html>
        """;

    [Fact]
    public async Task ExecuteAsync_TrimsInputSecuresHtmlAndSavesArtifact()
    {
        var client = new StubCopilotClient(ValidHtml);
        var store = new StubArtifactStore();
        var useCase = new GenerateVisualizationUseCase(
            client,
            new GeneratedHtmlDocumentProcessor(),
            store);

        var result = await useCase.ExecuteAsync(
            "  Explain Azure Functions  ",
            new AppSettings
            {
                Model = "  gpt-5  ",
                OutputDirectory = "generated",
            });

        Assert.Equal("Explain Azure Functions", client.Query);
        Assert.Equal("gpt-5", client.Model);
        Assert.Equal("generated", store.OutputDirectory);
        Assert.Contains("Content-Security-Policy", store.Html);
        Assert.Equal(store.Artifact, result);
    }

    [Fact]
    public async Task ExecuteAsync_UsesAutoModelWhenSettingIsBlank()
    {
        var client = new StubCopilotClient(ValidHtml);
        var useCase = new GenerateVisualizationUseCase(
            client,
            new GeneratedHtmlDocumentProcessor(),
            new StubArtifactStore());

        await useCase.ExecuteAsync("Compare two services", new AppSettings { Model = " " });

        Assert.Equal(AppSettings.DefaultModel, client.Model);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_RejectsBlankQuery(string query)
    {
        var client = new StubCopilotClient(ValidHtml);
        var useCase = new GenerateVisualizationUseCase(
            client,
            new GeneratedHtmlDocumentProcessor(),
            new StubArtifactStore());

        var exception = await Assert.ThrowsAsync<VisualizationGenerationException>(
            () => useCase.ExecuteAsync(query, new AppSettings()));

        Assert.Contains("Enter", exception.Message);
        Assert.Null(client.Query);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsOversizedQuery()
    {
        var client = new StubCopilotClient(ValidHtml);
        var useCase = new GenerateVisualizationUseCase(
            client,
            new GeneratedHtmlDocumentProcessor(),
            new StubArtifactStore());

        var query = new string('x', GenerateVisualizationUseCase.MaximumQueryLength + 1);

        await Assert.ThrowsAsync<VisualizationGenerationException>(
            () => useCase.ExecuteAsync(query, new AppSettings()));
        Assert.Null(client.Query);
    }

    private sealed class StubCopilotClient(string response) : ICopilotVisualizationClient
    {
        public string? Query { get; private set; }

        public string? Model { get; private set; }

        public Task<string> GenerateHtmlAsync(
            string query,
            string model,
            IProgress<GenerationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Query = query;
            Model = model;
            return Task.FromResult(response);
        }
    }

    private sealed class StubArtifactStore : IGeneratedArtifactStore
    {
        public VisualizationArtifact Artifact { get; } = new(
            "/tmp/azhst.html",
            new Uri("file:///tmp/azhst.html"));

        public string? Html { get; private set; }

        public string? OutputDirectory { get; private set; }

        public Task<VisualizationArtifact> SaveAsync(
            string html,
            string outputDirectory,
            CancellationToken cancellationToken = default)
        {
            Html = html;
            OutputDirectory = outputDirectory;
            return Task.FromResult(Artifact);
        }
    }
}
