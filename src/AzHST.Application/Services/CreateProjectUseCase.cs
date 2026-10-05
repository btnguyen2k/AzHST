using AzHST.Application.Abstractions;
using AzHST.Application.Exceptions;
using AzHST.Application.Models;

namespace AzHST.Application.Services;

public sealed class CreateProjectUseCase
{
    public const int MaximumQueryLength = 12_000;

    private readonly ICopilotVisualizationClient _copilotClient;
    private readonly ICopilotProjectConversation _conversation;
    private readonly GeneratedHtmlDocumentProcessor _documentProcessor;
    private readonly IProjectArtifactStore _artifactStore;
    private readonly IProjectRepository _repository;
    private readonly VisualizationArtifactIdGenerator _artifactIdGenerator;
    private readonly IOutputThemeCatalog _themes;
    private readonly TimeProvider _timeProvider;

    public CreateProjectUseCase(
        ICopilotVisualizationClient copilotClient,
        ICopilotProjectConversation conversation,
        GeneratedHtmlDocumentProcessor documentProcessor,
        IProjectArtifactStore artifactStore,
        IProjectRepository repository,
        VisualizationArtifactIdGenerator artifactIdGenerator,
        IOutputThemeCatalog themes,
        TimeProvider? timeProvider = null)
    {
        _copilotClient = copilotClient;
        _conversation = conversation;
        _documentProcessor = documentProcessor;
        _artifactStore = artifactStore;
        _repository = repository;
        _artifactIdGenerator = artifactIdGenerator;
        _themes = themes;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ProjectWorkspace> ExecuteAsync(
        string query,
        AppSettings settings,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalizedQuery = NormalizeQuery(query);
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

        var projectId = _artifactIdGenerator.Create(
            assessment.SuggestedSlug,
            normalizedQuery);
        var sessionId = $"azhst-project-{projectId}";
        var theme = _themes.GetHtmlTheme(settings.Themes.HtmlThemeId);
        var rawResponse = await _conversation.CreateAsync(
            sessionId,
            normalizedQuery,
            model,
            projectId,
            theme,
            progress,
            cancellationToken);

        VisualizationArtifact? savedArtifact = null;
        try
        {
            progress?.Report(new GenerationProgress(
                GenerationStage.Securing,
                "Validating and securing the generated page..."));

            var securedHtml = _documentProcessor.Process(rawResponse, theme);

            progress?.Report(new GenerationProgress(
                GenerationStage.Saving,
                "Saving the project..."));

            savedArtifact = await _artifactStore.SaveAsync(
                projectId,
                ProjectRevisionIds.Current,
                securedHtml,
                settings.OutputDirectory,
                overwrite: false,
                cancellationToken);
            var now = _timeProvider.GetUtcNow();
            var revision = new ProjectRevision(
                projectId,
                ProjectRevisionIds.Current,
                savedArtifact.FilePath,
                PresentationFilePath: null,
                ProjectGenerationStatus.Ready,
                now);
            var project = new Project(
                projectId,
                CreateTitle(normalizedQuery),
                normalizedQuery,
                sessionId,
                now,
                now,
                model,
                theme.Id,
                settings.Themes.PresentationThemeId,
                revision);

            await _repository.SaveAsync(project, cancellationToken);

            progress?.Report(new GenerationProgress(
                GenerationStage.Completed,
                "Project ready."));

            return new ProjectWorkspace(
                project,
                savedArtifact with { Html = securedHtml });
        }
        catch (Exception exception)
        {
            await CleanupFailedProjectAsync(
                projectId,
                sessionId,
                savedArtifact,
                exception);
            throw;
        }
    }

    private async Task CleanupFailedProjectAsync(
        string projectId,
        string sessionId,
        VisualizationArtifact? artifact,
        Exception originalException)
    {
        var placeholder = new Project(
            projectId,
            string.Empty,
            string.Empty,
            sessionId,
            DateTimeOffset.MinValue,
            DateTimeOffset.MinValue,
            string.Empty,
            string.Empty,
            string.Empty,
            new ProjectRevision(
                projectId,
                ProjectRevisionIds.Current,
                artifact?.FilePath ?? string.Empty,
                null,
                ProjectGenerationStatus.Ready,
                DateTimeOffset.MinValue));

        var cleanupErrors = new List<Exception>();
        if (artifact is not null)
        {
            try
            {
                await _artifactStore.DeleteAsync(
                    placeholder,
                    CancellationToken.None);
            }
            catch (Exception cleanupException)
            {
                cleanupErrors.Add(cleanupException);
            }
        }

        try
        {
            await _conversation.DeleteAsync(sessionId, CancellationToken.None);
        }
        catch (Exception cleanupException)
        {
            cleanupErrors.Add(cleanupException);
        }

        if (cleanupErrors.Count > 0)
        {
            throw new AggregateException(
                "Project creation failed and cleanup did not complete.",
                [originalException, .. cleanupErrors]);
        }
    }

    private static string NormalizeQuery(string? query)
    {
        var normalized = query?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            throw new VisualizationGenerationException(
                "Enter an Azure or Microsoft services question first.");
        }

        if (normalized.Length > MaximumQueryLength)
        {
            throw new VisualizationGenerationException(
                $"The question is too long. Keep it under {MaximumQueryLength:N0} characters.");
        }

        return normalized;
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

    private static string CreateTitle(string query)
    {
        const int maximumLength = 100;
        var singleLine = string.Join(
            " ",
            query.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries
                    | StringSplitOptions.TrimEntries));
        return singleLine.Length <= maximumLength
            ? singleLine
            : $"{singleLine[..(maximumLength - 3)].TrimEnd()}...";
    }
}
