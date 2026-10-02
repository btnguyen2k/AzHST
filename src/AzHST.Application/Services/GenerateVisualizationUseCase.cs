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
    private readonly VisualizationArtifactIdGenerator _artifactIdGenerator;
    private readonly IOutputThemeCatalog _themes;

    public GenerateVisualizationUseCase(
        ICopilotVisualizationClient copilotClient,
        GeneratedHtmlDocumentProcessor documentProcessor,
        IGeneratedArtifactStore artifactStore,
        VisualizationArtifactIdGenerator artifactIdGenerator,
        IOutputThemeCatalog themes)
    {
        _copilotClient = copilotClient;
        _documentProcessor = documentProcessor;
        _artifactStore = artifactStore;
        _artifactIdGenerator = artifactIdGenerator;
        _themes = themes;
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

        var model = CopilotModelSelection.Normalize(settings.Model);
        var assessment = await _copilotClient.AssessQueryAsync(
            normalizedQuery,
            model,
            progress,
            cancellationToken);

        if (!assessment.IsValid)
        {
            throw new InvalidVisualizationQueryException(
                NormalizeInvalidQueryMessage(assessment.Message));
        }

        var visualizationId = _artifactIdGenerator.Create(
            assessment.SuggestedSlug,
            normalizedQuery);
        var theme = _themes.GetHtmlTheme(settings.Themes.HtmlThemeId);

        var rawResponse = await _copilotClient.GenerateHtmlAsync(
            normalizedQuery,
            model,
            visualizationId,
            theme,
            progress,
            cancellationToken);

        progress?.Report(new GenerationProgress(
            GenerationStage.Securing,
            "Validating and securing the generated page..."));

        var securedHtml = _documentProcessor.Process(rawResponse, theme);

        progress?.Report(new GenerationProgress(
            GenerationStage.Saving,
            "Saving the visualization..."));

        var artifact = await _artifactStore.SaveAsync(
            visualizationId,
            securedHtml,
            settings.OutputDirectory,
            cancellationToken);

        progress?.Report(new GenerationProgress(
            GenerationStage.Completed,
            "Visualization ready."));

        return artifact with
        {
            Html = securedHtml,
        };
    }

    private static string NormalizeInvalidQueryMessage(string message)
    {
        const string defaultMessage =
            "Ask about Azure or Microsoft services using a question that can be explained visually, such as a service flow, comparison, or architecture.";

        if (string.IsNullOrWhiteSpace(message))
        {
            return defaultMessage;
        }

        var normalized = message.Trim();
        return normalized.Length <= 500
            ? normalized
            : normalized[..500];
    }
}
