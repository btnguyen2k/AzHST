using AzHST.Application.Abstractions;
using AzHST.Application.Exceptions;
using AzHST.Application.Models;

namespace AzHST.Application.Services;

public sealed class GenerateVisualizationUseCase
{
    public const int MaximumQueryLength = 12_000;

    private readonly ICopilotVisualizationClient _copilotClient;
    private readonly GeneratedHtmlDocumentProcessor _documentProcessor;
    private readonly IGeneratedArtifactStore _artifactStore;

    public GenerateVisualizationUseCase(
        ICopilotVisualizationClient copilotClient,
        GeneratedHtmlDocumentProcessor documentProcessor,
        IGeneratedArtifactStore artifactStore)
    {
        _copilotClient = copilotClient;
        _documentProcessor = documentProcessor;
        _artifactStore = artifactStore;
    }

    public async Task<VisualizationArtifact> ExecuteAsync(
        string query,
        AppSettings settings,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalizedQuery = query?.Trim();
        if (string.IsNullOrEmpty(normalizedQuery))
        {
            throw new VisualizationGenerationException("Enter an Azure or Microsoft services question first.");
        }

        if (normalizedQuery.Length > MaximumQueryLength)
        {
            throw new VisualizationGenerationException(
                $"The question is too long. Keep it under {MaximumQueryLength:N0} characters.");
        }

        var model = string.IsNullOrWhiteSpace(settings.Model)
            ? AppSettings.DefaultModel
            : settings.Model.Trim();

        var rawResponse = await _copilotClient.GenerateHtmlAsync(
            normalizedQuery,
            model,
            progress,
            cancellationToken);

        progress?.Report(new GenerationProgress(
            GenerationStage.Securing,
            "Validating and securing the generated page..."));

        var securedHtml = _documentProcessor.Process(rawResponse);

        progress?.Report(new GenerationProgress(
            GenerationStage.Saving,
            "Saving the visualization..."));

        var artifact = await _artifactStore.SaveAsync(
            securedHtml,
            settings.OutputDirectory,
            cancellationToken);

        progress?.Report(new GenerationProgress(
            GenerationStage.Completed,
            "Visualization ready."));

        return artifact;
    }
}
