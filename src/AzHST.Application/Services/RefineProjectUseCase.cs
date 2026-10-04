using AzHST.Application.Abstractions;
using AzHST.Application.Exceptions;
using AzHST.Application.Models;

namespace AzHST.Application.Services;

public sealed class RefineProjectUseCase
{
    public const int MaximumFollowUpLength = 12_000;

    private readonly ICopilotProjectConversation _conversation;
    private readonly GeneratedHtmlDocumentProcessor _documentProcessor;
    private readonly IProjectArtifactStore _artifactStore;
    private readonly IProjectRepository _repository;
    private readonly IOutputThemeCatalog _themes;
    private readonly TimeProvider _timeProvider;

    public RefineProjectUseCase(
        ICopilotProjectConversation conversation,
        GeneratedHtmlDocumentProcessor documentProcessor,
        IProjectArtifactStore artifactStore,
        IProjectRepository repository,
        IOutputThemeCatalog themes,
        TimeProvider? timeProvider = null)
    {
        _conversation = conversation;
        _documentProcessor = documentProcessor;
        _artifactStore = artifactStore;
        _repository = repository;
        _themes = themes;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ProjectWorkspace> ExecuteAsync(
        string projectId,
        string followUpRequest,
        AppSettings settings,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalizedRequest = NormalizeRequest(followUpRequest);
        var project = await _repository.GetAsync(projectId, cancellationToken)
            ?? throw new ProjectNotFoundException(projectId);
        var currentArtifact = await _artifactStore.LoadAsync(
            project,
            cancellationToken);
        var model = CopilotModelSelection.Normalize(settings.Model);
        var theme = _themes.GetHtmlTheme(settings.Themes.HtmlThemeId);

        var rawResponse = await _conversation.RefineAsync(
            project.CopilotSessionId,
            project.OriginalQuery,
            normalizedRequest,
            model,
            project.Id,
            theme,
            progress,
            cancellationToken);

        progress?.Report(new GenerationProgress(
            GenerationStage.Securing,
            "Validating and securing the updated page..."));

        var securedHtml = _documentProcessor.Process(rawResponse, theme);

        progress?.Report(new GenerationProgress(
            GenerationStage.Saving,
            "Updating revision 000..."));

        var artifact = await _artifactStore.SaveAsync(
            project.Id,
            ProjectRevisionIds.Current,
            securedHtml,
            GetOutputRoot(project.Revision.HtmlFilePath),
            overwrite: true,
            cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var updatedProject = project with
        {
            UpdatedUtc = now,
            SelectedModelId = model,
            HtmlThemeId = theme.Id,
            PresentationThemeId = settings.Themes.PresentationThemeId,
            Revision = project.Revision with
            {
                HtmlFilePath = artifact.FilePath,
                PresentationFilePath = null,
                GenerationStatus = ProjectGenerationStatus.Ready,
                UpdatedUtc = now,
            },
        };

        try
        {
            await _repository.SaveAsync(updatedProject, cancellationToken);
        }
        catch (Exception exception)
        {
            try
            {
                await _artifactStore.SaveAsync(
                    project.Id,
                    ProjectRevisionIds.Current,
                    currentArtifact.Html,
                    GetOutputRoot(project.Revision.HtmlFilePath),
                    overwrite: true,
                    CancellationToken.None);
            }
            catch (Exception rollbackException)
            {
                throw new AggregateException(
                    "The project update could not be recorded and the previous HTML could not be restored.",
                    exception,
                    rollbackException);
            }

            throw;
        }

        progress?.Report(new GenerationProgress(
            GenerationStage.Completed,
            "Project updated."));

        return new ProjectWorkspace(
            updatedProject,
            artifact with { Html = securedHtml });
    }

    private static string GetOutputRoot(string htmlFilePath)
    {
        var revisionDirectory = Path.GetDirectoryName(htmlFilePath);
        var projectDirectory = revisionDirectory is null
            ? null
            : Path.GetDirectoryName(revisionDirectory);
        return projectDirectory is null
            ? throw new InvalidDataException(
                "The project HTML path does not contain a generated output root.")
            : Path.GetDirectoryName(projectDirectory)
                ?? throw new InvalidDataException(
                    "The project HTML path does not contain a generated output root.");
    }

    private static string NormalizeRequest(string? followUpRequest)
    {
        var normalized = followUpRequest?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            throw new VisualizationGenerationException(
                "Describe how you want to refine the visualization.");
        }

        if (normalized.Length > MaximumFollowUpLength)
        {
            throw new VisualizationGenerationException(
                $"The follow-up request is too long. Keep it under {MaximumFollowUpLength:N0} characters.");
        }

        return normalized;
    }
}
