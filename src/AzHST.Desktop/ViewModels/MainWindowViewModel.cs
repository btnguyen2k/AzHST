using System.Collections.ObjectModel;
using AzHST.Application.Abstractions;
using AzHST.Application.Exceptions;
using AzHST.Application.Models;
using AzHST.Application.Services;
using AzHST.Desktop.Models;
using AzHST.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AzHST.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly CreateProjectUseCase _createProjectUseCase;
    private readonly OpenProjectUseCase _openProjectUseCase;
    private readonly ListProjectsUseCase _listProjectsUseCase;
    private readonly RefineProjectUseCase _refineProjectUseCase;
    private readonly RenameProjectUseCase _renameProjectUseCase;
    private readonly DeleteProjectUseCase _deleteProjectUseCase;
    private readonly RecordProjectPresentationUseCase
        _recordProjectPresentationUseCase;
    private readonly GeneratePresentationUseCase _presentationUseCase;
    private readonly SampleQueryUseCase _sampleQueryUseCase;
    private readonly IGitHubAuthenticationService _authenticationService;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IOutputThemeCatalog _themes;
    private readonly IExternalBrowser _browser;
    private readonly IExternalFileLauncher _fileLauncher;
    private readonly IClipboardService _clipboardService;
    private readonly ISettingsDialogService _settingsDialogService;
    private readonly IGitHubLoginDialogService _loginDialogService;
    private readonly IAboutDialogService _aboutDialogService;
    private readonly WebViewAvailability _webViewAvailability;
    private readonly bool _skipGitHubSignInAtStartup;
    private Project? _activeProject;
    private VisualizationArtifact? _visualizationArtifact;
    private string _generatedQuery = string.Empty;
    private bool _sampleQueryStoreInitialized;
    private bool _initialized;

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private string _projectTitle = string.Empty;

    [ObservableProperty]
    private bool _hasActiveProject;

    [ObservableProperty]
    private bool _isDeleteConfirmationVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private bool _isBuildingPresentation;

    [ObservableProperty]
    private bool _isAuthenticated;

    [ObservableProperty]
    private string _authenticationMessage = "Checking GitHub authentication...";

    [ObservableProperty]
    private string _gitHubSignInButtonText = "GitHub sign-in instructions";

    [ObservableProperty]
    private string _statusMessage = "Ask a question to create an Azure visualization.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOperationError))]
    private string _operationErrorMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreview))]
    [NotifyPropertyChangedFor(nameof(HasEmbeddedPreview))]
    [NotifyPropertyChangedFor(nameof(ShowPreviewFallback))]
    [NotifyPropertyChangedFor(nameof(ShowWelcome))]
    [NotifyPropertyChangedFor(nameof(EmbeddedPreviewUri))]
    private Uri? _previewUri;

    [ObservableProperty]
    private string? _generatedFilePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPresentation))]
    private Uri? _presentationUri;

    [ObservableProperty]
    private string? _presentationFilePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CopilotModelName))]
    [NotifyPropertyChangedFor(nameof(HtmlThemeName))]
    [NotifyPropertyChangedFor(nameof(PresentationThemeName))]
    [NotifyPropertyChangedFor(nameof(ResultOpeningMode))]
    private AppSettings _settings = new();

    public MainWindowViewModel(
        CreateProjectUseCase createProjectUseCase,
        OpenProjectUseCase openProjectUseCase,
        ListProjectsUseCase listProjectsUseCase,
        RefineProjectUseCase refineProjectUseCase,
        RenameProjectUseCase renameProjectUseCase,
        DeleteProjectUseCase deleteProjectUseCase,
        RecordProjectPresentationUseCase recordProjectPresentationUseCase,
        GeneratePresentationUseCase presentationUseCase,
        SampleQueryUseCase sampleQueryUseCase,
        IGitHubAuthenticationService authenticationService,
        ISettingsRepository settingsRepository,
        IOutputThemeCatalog themes,
        IExternalBrowser browser,
        IExternalFileLauncher fileLauncher,
        IClipboardService clipboardService,
        ISettingsDialogService settingsDialogService,
        IGitHubLoginDialogService loginDialogService,
        IAboutDialogService aboutDialogService,
        ApplicationIdentity applicationIdentity,
        WebViewAvailability webViewAvailability,
        bool skipGitHubSignInAtStartup = false)
    {
        _createProjectUseCase = createProjectUseCase;
        _openProjectUseCase = openProjectUseCase;
        _listProjectsUseCase = listProjectsUseCase;
        _refineProjectUseCase = refineProjectUseCase;
        _renameProjectUseCase = renameProjectUseCase;
        _deleteProjectUseCase = deleteProjectUseCase;
        _recordProjectPresentationUseCase =
            recordProjectPresentationUseCase;
        _presentationUseCase = presentationUseCase;
        _sampleQueryUseCase = sampleQueryUseCase;
        _authenticationService = authenticationService;
        _settingsRepository = settingsRepository;
        _themes = themes;
        _browser = browser;
        _fileLauncher = fileLauncher;
        _clipboardService = clipboardService;
        _settingsDialogService = settingsDialogService;
        _loginDialogService = loginDialogService;
        _aboutDialogService = aboutDialogService;
        ApplicationIdentityText = applicationIdentity.DisplayText;
        _webViewAvailability = webViewAvailability;
        _skipGitHubSignInAtStartup = skipGitHubSignInAtStartup;
    }

    public bool HasPreview => PreviewUri is not null;

    public bool HasPresentation => PresentationUri is not null;

    public bool HasEmbeddedPreview => HasPreview && _webViewAvailability.IsAvailable;

    public bool ShowPreviewFallback => HasPreview && !_webViewAvailability.IsAvailable;

    public bool ShowWelcome => !HasPreview;

    public bool IsIdle => !IsBusy;

    public bool HasOperationError =>
        !string.IsNullOrWhiteSpace(OperationErrorMessage);

    public ObservableCollection<SampleQueryOptionViewModel> SampleQueries { get; } = [];

    public ObservableCollection<ProjectOptionViewModel> Projects { get; } = [];

    public bool HasProjects => Projects.Count > 0;

    public bool HasNoProjects => !HasProjects;

    public string PrimaryActionText => HasActiveProject
        ? "Refine visualization"
        : "Create project";

    public string QueryPlaceholder => HasActiveProject
        ? "Describe what to add, remove, compare, clarify, or redesign..."
        : "Ask about an Azure service, comparison, integration, or target architecture...";

    public Uri? EmbeddedPreviewUri =>
        _webViewAvailability.IsAvailable ? PreviewUri : null;

    public string WebViewAvailabilityMessage => _webViewAvailability.Message;

    public string ApplicationIdentityText { get; }

    public string CopilotModelName =>
        CopilotModelSelection.IsAutomatic(Settings.Model)
            ? "Automatic"
            : Settings.Model;

    public string HtmlThemeName => ResolveThemeName(
        _themes.HtmlThemes,
        Settings.Themes.HtmlThemeId);

    public string PresentationThemeName => ResolveThemeName(
        _themes.PresentationThemes,
        Settings.Themes.PresentationThemeId);

    public string ResultOpeningMode => Settings.OpenResultsInExternalBrowser
        ? "Preview + browser"
        : "Embedded preview";

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        IsBusy = true;
        StatusMessage = "Loading configuration...";
        string? configurationError = null;
        string? authenticationError = null;
        string? sampleQueryNotice = null;
        string? projectNotice = null;

        try
        {
            StatusMessage = "Checking the local sample query database...";
            var initialization = await _sampleQueryUseCase.InitializeAsync();
            _sampleQueryStoreInitialized = true;
            await LoadHomeSamplesAsync();

            if (initialization.WasReset)
            {
                sampleQueryNotice =
                    "The local sample query database was invalid and was reset.";
            }
        }
        catch (Exception exception)
        {
            sampleQueryNotice =
                $"Sample query database error: {exception.Message}";
        }

        try
        {
            StatusMessage = "Loading local projects...";
            await LoadProjectsAsync();
        }
        catch (Exception exception)
        {
            projectNotice = $"Project history error: {exception.Message}";
        }

        try
        {
            Settings = await _settingsRepository.LoadAsync();
        }
        catch (Exception exception)
        {
            configurationError = $"Configuration error: {exception.Message}";
        }

        try
        {
            var authenticationStatus = _skipGitHubSignInAtStartup
                ? new AuthenticationStatus(
                    false,
                    "Not signed in. Open the GitHub sign-in instructions to continue.")
                : await _authenticationService.GetStatusAsync();

            ApplyAuthenticationStatus(authenticationStatus);

            if (IsAuthenticated && _sampleQueryStoreInitialized)
            {
                try
                {
                    StatusMessage = "Checking whether sample questions need refreshing...";
                    if (await _sampleQueryUseCase.RefreshIfStaleAsync())
                    {
                        await LoadHomeSamplesAsync();
                    }
                }
                catch (Exception exception)
                {
                    sampleQueryNotice =
                        $"Could not refresh sample questions; existing suggestions remain available. {exception.Message}";
                }
            }
        }
        catch (Exception exception)
        {
            AuthenticationMessage = $"Authentication check failed: {exception.Message}";
            authenticationError =
                $"GitHub authentication check failed: {exception.Message}";
        }
        finally
        {
            var readyStatus = IsAuthenticated
                ? "Ready. Ask an Azure or Microsoft services question."
                : "Sign in with GitHub to enable visualization generation.";
            StatusMessage = AppendNotice(
                configurationError ?? authenticationError ?? readyStatus,
                JoinNotices(sampleQueryNotice, projectNotice));
            IsBusy = false;
        }
    }

    private static string? JoinNotices(params string?[] notices)
    {
        var available = notices
            .Where(notice => !string.IsNullOrWhiteSpace(notice))
            .ToArray();
        return available.Length == 0
            ? null
            : string.Join(" ", available);
    }

    private static string AppendNotice(string message, string? notice)
    {
        return string.IsNullOrWhiteSpace(notice)
            ? message
            : $"{message} {notice}";
    }

    private async Task LoadHomeSamplesAsync(
        CancellationToken cancellationToken = default)
    {
        var samples = await _sampleQueryUseCase.GetHomeSuggestionsAsync(
            cancellationToken);
        var options = samples
            .Select(sample => new SampleQueryOptionViewModel(sample, UseExample))
            .ToArray();

        SampleQueries.Clear();
        foreach (var option in options)
        {
            SampleQueries.Add(option);
        }
    }

    private bool CanRefreshSampleQueries()
    {
        return !IsBusy && _sampleQueryStoreInitialized;
    }

    [RelayCommand(CanExecute = nameof(CanRefreshSampleQueries))]
    private async Task RefreshSampleQueriesAsync()
    {
        IsBusy = true;
        OperationErrorMessage = string.Empty;
        StatusMessage = "Refreshing sample questions...";

        try
        {
            await LoadHomeSamplesAsync();
            StatusMessage = "Sample questions refreshed.";
        }
        catch (Exception exception)
        {
            StatusMessage =
                $"Could not refresh sample questions: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadProjectsAsync(
        CancellationToken cancellationToken = default)
    {
        var projects = await _listProjectsUseCase.ExecuteAsync(
            cancellationToken);
        var activeProjectId = _activeProject?.Id;
        var options = projects
            .Select(project => new ProjectOptionViewModel(
                project,
                string.Equals(
                    project.Id,
                    activeProjectId,
                    StringComparison.Ordinal),
                OpenProjectAsync))
            .ToArray();

        Projects.Clear();
        foreach (var option in options)
        {
            Projects.Add(option);
        }

        OnPropertyChanged(nameof(HasProjects));
        OnPropertyChanged(nameof(HasNoProjects));
    }

    private static string ResolveThemeName(
        IReadOnlyList<OutputThemeOption> options,
        string selectedId)
    {
        return options.FirstOrDefault(
                option => string.Equals(
                    option.Id,
                    selectedId,
                    StringComparison.OrdinalIgnoreCase))
            ?.DisplayName
            ?? selectedId;
    }

    private bool CanGenerate()
    {
        return !IsBusy
            && IsAuthenticated
            && !string.IsNullOrWhiteSpace(Query);
    }

    [RelayCommand(CanExecute = nameof(CanGenerate), IncludeCancelCommand = true)]
    private async Task GenerateAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        IsGenerating = true;
        OperationErrorMessage = string.Empty;
        var progressEnabled = 1;
        var lastProgressMessage = HasActiveProject
            ? "Preparing project refinement..."
            : "Preparing project creation...";
        StatusMessage = lastProgressMessage;
        var submittedQuery = Query;
        var wasRefinement = _activeProject is not null;
        var progress = new Progress<GenerationProgress>(
            update =>
            {
                if (Volatile.Read(ref progressEnabled) == 0)
                {
                    return;
                }

                lastProgressMessage = update.Message;
                StatusMessage = update.Message;
            });

        try
        {
            var workspace = _activeProject is null
                ? await _createProjectUseCase.ExecuteAsync(
                    submittedQuery,
                    Settings,
                    progress,
                    cancellationToken)
                : await _refineProjectUseCase.ExecuteAsync(
                    _activeProject.Id,
                    submittedQuery,
                    Settings,
                    progress,
                    cancellationToken);

            ApplyWorkspace(workspace);
            Query = string.Empty;
            await LoadProjectsAsync(cancellationToken);
            Interlocked.Exchange(ref progressEnabled, 0);

            if (Settings.OpenResultsInExternalBrowser || !_webViewAvailability.IsAvailable)
            {
                _browser.Open(workspace.Visualization.FileUri);
                StatusMessage = _webViewAvailability.IsAvailable
                    ? wasRefinement
                        ? "Project updated and opened in the default browser."
                        : "Project created and opened in the default browser."
                    : "Embedded preview is unavailable; opened the project visualization in the default browser.";
            }
            else
            {
                StatusMessage = "Project ready in the embedded preview.";
            }
        }
        catch (OperationCanceledException)
        {
            Interlocked.Exchange(ref progressEnabled, 0);
            StatusMessage = "Generation cancelled.";
        }
        catch (InvalidVisualizationQueryException exception)
        {
            Interlocked.Exchange(ref progressEnabled, 0);
            StatusMessage = exception.Message;
        }
        catch (Exception exception)
        {
            Interlocked.Exchange(ref progressEnabled, 0);
            StatusMessage =
                "Project generation failed. Review the error below and retry.";
            OperationErrorMessage = BuildOperationError(
                "Project generation failed.",
                lastProgressMessage,
                exception);
        }
        finally
        {
            Interlocked.Exchange(ref progressEnabled, 0);
            IsGenerating = false;
            IsBusy = false;
        }
    }

    private bool CanBuildPowerPoint()
    {
        return !IsBusy
            && IsAuthenticated
            && HasPreview;
    }

    [RelayCommand(CanExecute = nameof(CanBuildPowerPoint), IncludeCancelCommand = true)]
    private async Task BuildPowerPointAsync(CancellationToken cancellationToken)
    {
        if (_visualizationArtifact is null || _generatedQuery.Length == 0)
        {
            StatusMessage =
                "PowerPoint generation could not start. Review the error below.";
            OperationErrorMessage =
                "The current visualization context is unavailable. Generate the visualization again, then retry Build PowerPoint.";
            return;
        }

        IsBusy = true;
        IsBuildingPresentation = true;
        OperationErrorMessage = string.Empty;
        var progressEnabled = 1;
        var lastProgressMessage = "Preparing PowerPoint generation...";
        StatusMessage = lastProgressMessage;
        var progress = new Progress<GenerationProgress>(
            update =>
            {
                if (Volatile.Read(ref progressEnabled) == 0)
                {
                    return;
                }

                lastProgressMessage = update.Message;
                StatusMessage = update.Message;
            });

        try
        {
            var artifact = await _presentationUseCase.ExecuteAsync(
                _generatedQuery,
                _visualizationArtifact,
                Settings,
                progress,
                cancellationToken);

            if (_activeProject is not null)
            {
                _activeProject =
                    await _recordProjectPresentationUseCase.ExecuteAsync(
                        _activeProject.Id,
                        artifact,
                        Settings.Themes.PresentationThemeId,
                        cancellationToken);
                await LoadProjectsAsync(cancellationToken);
            }

            PresentationFilePath = artifact.FilePath;
            PresentationUri = artifact.FileUri;
            Interlocked.Exchange(ref progressEnabled, 0);
            StatusMessage =
                $"PowerPoint ready with {artifact.SlideCount} slides. Copy its path or open its folder below.";
        }
        catch (OperationCanceledException)
        {
            Interlocked.Exchange(ref progressEnabled, 0);
            StatusMessage = "PowerPoint generation cancelled.";
        }
        catch (Exception exception)
        {
            Interlocked.Exchange(ref progressEnabled, 0);
            StatusMessage =
                "PowerPoint generation failed. Review the error below and retry.";
            OperationErrorMessage = BuildOperationError(
                "PowerPoint generation failed.",
                lastProgressMessage,
                exception);
        }
        finally
        {
            Interlocked.Exchange(ref progressEnabled, 0);
            IsBuildingPresentation = false;
            IsBusy = false;
        }
    }

    private static string BuildOperationError(
        string heading,
        string lastProgressMessage,
        Exception exception)
    {
        var messages = new List<string>();
        var pending = new Queue<Exception>();
        pending.Enqueue(exception);

        while (pending.Count > 0 && messages.Count < 6)
        {
            var current = pending.Dequeue();
            var message = current.Message.Trim();
            if (message.Length > 0
                && !messages.Contains(
                    message,
                    StringComparer.Ordinal))
            {
                messages.Add(message);
            }

            if (current is AggregateException aggregateException)
            {
                foreach (var innerException in aggregateException.InnerExceptions)
                {
                    pending.Enqueue(innerException);
                }
            }
            else if (current.InnerException is not null)
            {
                pending.Enqueue(current.InnerException);
            }
        }

        var details = messages.Count > 0
            ? string.Join(Environment.NewLine, messages)
            : exception.GetType().Name;
        return $"{heading}{Environment.NewLine}Last step: {lastProgressMessage}{Environment.NewLine}{details}";
    }

    private bool CanRunUiAction()
    {
        return !IsBusy;
    }

    [RelayCommand(CanExecute = nameof(CanRunUiAction))]
    private async Task GitHubSignInAsync()
    {
        IsBusy = true;

        try
        {
            if (!IsAuthenticated)
            {
                StatusMessage = "Run GitHub CLI sign-in in your terminal, then confirm when it is complete.";
                var shouldCheckStatus = await _loginDialogService.ShowAsync();
                if (!shouldCheckStatus)
                {
                    StatusMessage = "GitHub sign-in check cancelled.";
                    return;
                }
            }

            StatusMessage = "Checking GitHub CLI authentication...";
            var authenticationStatus = await _authenticationService.GetStatusAsync();
            ApplyAuthenticationStatus(authenticationStatus);
            string? sampleQueryNotice = null;

            if (authenticationStatus.IsAuthenticated
                && _sampleQueryStoreInitialized)
            {
                try
                {
                    StatusMessage = "Checking whether sample questions need refreshing...";
                    if (await _sampleQueryUseCase.RefreshIfStaleAsync())
                    {
                        await LoadHomeSamplesAsync();
                    }
                }
                catch (Exception exception)
                {
                    sampleQueryNotice =
                        $"Could not refresh sample questions; existing suggestions remain available. {exception.Message}";
                }
            }

            var authenticationMessage = authenticationStatus.IsAuthenticated
                ? "GitHub sign-in confirmed. Copilot is ready."
                : authenticationStatus.Message;
            StatusMessage = AppendNotice(
                authenticationMessage,
                sampleQueryNotice);
        }
        catch (Exception exception)
        {
            StatusMessage = $"GitHub status check failed: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunUiAction))]
    private async Task OpenSettingsAsync()
    {
        try
        {
            var updatedSettings = await _settingsDialogService.ShowAsync(Settings);
            if (updatedSettings is null)
            {
                return;
            }

            await _settingsRepository.SaveAsync(updatedSettings);
            Settings = await _settingsRepository.LoadAsync();
            StatusMessage = "Settings saved.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Could not save settings: {exception.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunUiAction))]
    private async Task OpenAboutAsync()
    {
        try
        {
            await _aboutDialogService.ShowAsync();
        }
        catch (Exception exception)
        {
            StatusMessage =
                $"Could not open application information: {exception.Message}";
        }
    }

    private async Task OpenProjectAsync(string projectId)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        OperationErrorMessage = string.Empty;
        StatusMessage = "Opening project...";

        try
        {
            var workspace = await _openProjectUseCase.ExecuteAsync(projectId);
            ApplyWorkspace(workspace);
            await LoadProjectsAsync();
            StatusMessage =
                "Project opened. Enter a follow-up request to refine revision 000.";
        }
        catch (Exception exception)
        {
            StatusMessage =
                "Could not open the project. Review the error below.";
            OperationErrorMessage = BuildOperationError(
                "Project open failed.",
                "Loading the saved project",
                exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRenameProject()
    {
        return !IsBusy
            && _activeProject is not null
            && !string.IsNullOrWhiteSpace(ProjectTitle)
            && !string.Equals(
                _activeProject.Title,
                ProjectTitle.Trim(),
                StringComparison.Ordinal);
    }

    [RelayCommand(CanExecute = nameof(CanRenameProject))]
    private async Task RenameProjectAsync()
    {
        if (_activeProject is null)
        {
            return;
        }

        IsBusy = true;
        OperationErrorMessage = string.Empty;

        try
        {
            _activeProject = await _renameProjectUseCase.ExecuteAsync(
                _activeProject.Id,
                ProjectTitle);
            ProjectTitle = _activeProject.Title;
            await LoadProjectsAsync();
            StatusMessage = "Project title saved.";
        }
        catch (Exception exception)
        {
            StatusMessage =
                "Could not rename the project. Review the error below.";
            OperationErrorMessage = BuildOperationError(
                "Project rename failed.",
                "Saving the project title",
                exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRequestProjectDeletion()
    {
        return !IsBusy
            && _activeProject is not null
            && !IsDeleteConfirmationVisible;
    }

    [RelayCommand(CanExecute = nameof(CanRequestProjectDeletion))]
    private void RequestProjectDeletion()
    {
        IsDeleteConfirmationVisible = true;
        StatusMessage =
            "Confirm deletion to remove this project, its generated files, and its Copilot conversation.";
    }

    private bool CanCancelProjectDeletion()
    {
        return !IsBusy && IsDeleteConfirmationVisible;
    }

    [RelayCommand(CanExecute = nameof(CanCancelProjectDeletion))]
    private void CancelProjectDeletion()
    {
        IsDeleteConfirmationVisible = false;
        StatusMessage = "Project deletion cancelled.";
    }

    private bool CanConfirmProjectDeletion()
    {
        return !IsBusy
            && _activeProject is not null
            && IsDeleteConfirmationVisible;
    }

    [RelayCommand(CanExecute = nameof(CanConfirmProjectDeletion))]
    private async Task ConfirmProjectDeletionAsync()
    {
        if (_activeProject is null)
        {
            return;
        }

        IsBusy = true;
        OperationErrorMessage = string.Empty;
        var projectId = _activeProject.Id;

        try
        {
            await _deleteProjectUseCase.ExecuteAsync(projectId);
            ResetCurrentVisualization();
            await LoadProjectsAsync();
            StatusMessage = "Project deleted.";
        }
        catch (Exception exception)
        {
            StatusMessage =
                "Could not delete the project. Review the error below.";
            OperationErrorMessage = BuildOperationError(
                "Project deletion failed.",
                "Removing the project and its Copilot conversation",
                exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanGoHome()
    {
        return !IsBusy && HasActiveProject;
    }

    [RelayCommand(CanExecute = nameof(CanGoHome))]
    private async Task GoHomeAsync()
    {
        IsBusy = true;
        ResetCurrentVisualization();
        string? refreshNotice = null;

        try
        {
            if (_sampleQueryStoreInitialized)
            {
                if (IsAuthenticated)
                {
                    try
                    {
                        StatusMessage =
                            "Checking whether sample questions need refreshing...";
                        await _sampleQueryUseCase.RefreshIfStaleAsync();
                    }
                    catch (Exception exception)
                    {
                        refreshNotice =
                            $"Could not refresh sample questions; existing suggestions remain available. {exception.Message}";
                    }
                }

                await LoadHomeSamplesAsync();
            }

            StatusMessage = AppendNotice(
                "New project ready. Choose a sample or ask your own Azure question.",
                refreshNotice);
        }
        catch (Exception exception)
        {
            StatusMessage =
                $"Could not load sample questions: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ResetCurrentVisualization()
    {
        _activeProject = null;
        _visualizationArtifact = null;
        _generatedQuery = string.Empty;
        Query = string.Empty;
        ProjectTitle = string.Empty;
        HasActiveProject = false;
        IsDeleteConfirmationVisible = false;
        GeneratedFilePath = null;
        PreviewUri = null;
        PresentationFilePath = null;
        PresentationUri = null;
        BuildPowerPointCommand.NotifyCanExecuteChanged();
    }

    private void ApplyWorkspace(ProjectWorkspace workspace)
    {
        _activeProject = workspace.Project;
        _visualizationArtifact = workspace.Visualization;
        _generatedQuery = workspace.Project.OriginalQuery;
        Query = string.Empty;
        ProjectTitle = workspace.Project.Title;
        HasActiveProject = true;
        IsDeleteConfirmationVisible = false;
        GeneratedFilePath = workspace.Visualization.FilePath;
        PreviewUri = workspace.Visualization.FileUri;

        var presentationFile = workspace.Project.Revision.PresentationFilePath;
        if (!string.IsNullOrWhiteSpace(presentationFile)
            && File.Exists(presentationFile))
        {
            PresentationFilePath = presentationFile;
            PresentationUri = new Uri(Path.GetFullPath(presentationFile));
        }
        else
        {
            PresentationFilePath = null;
            PresentationUri = null;
        }

        Settings = Settings with
        {
            Model = workspace.Project.SelectedModelId,
            Themes = Settings.Themes with
            {
                HtmlThemeId = workspace.Project.HtmlThemeId,
                PresentationThemeId =
                    workspace.Project.PresentationThemeId,
            },
        };
        BuildPowerPointCommand.NotifyCanExecuteChanged();
    }

    private bool CanOpenExternal()
    {
        return !IsBusy && PreviewUri is not null;
    }

    public void OpenExternalSource(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!PresentationSourcePolicy.TryNormalizeUrl(
                uri.AbsoluteUri,
                out var normalizedUrl))
        {
            StatusMessage =
                "Blocked a source link outside the approved documentation hosts.";
            return;
        }

        try
        {
            _browser.Open(new Uri(normalizedUrl, UriKind.Absolute));
            StatusMessage = "Opened the source in the default browser.";
        }
        catch (Exception exception)
        {
            StatusMessage =
                $"Could not open the source link: {exception.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenExternal))]
    private void OpenExternal()
    {
        if (PreviewUri is null)
        {
            return;
        }

        try
        {
            _browser.Open(PreviewUri);
            StatusMessage = "Opened the visualization in the default browser.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Could not open the browser: {exception.Message}";
        }
    }

    private bool CanOpenPresentation()
    {
        return !IsBusy && PresentationUri is not null;
    }

    [RelayCommand(CanExecute = nameof(CanOpenPresentation))]
    private void OpenPresentation()
    {
        if (PresentationUri is null)
        {
            return;
        }

        try
        {
            _fileLauncher.Open(PresentationUri);
            StatusMessage =
                "Opened the PowerPoint presentation in the default application.";
        }
        catch (Exception exception)
        {
            StatusMessage =
                $"Could not open the PowerPoint presentation: {exception.Message}";
        }
    }

    private bool CanUsePresentationFile()
    {
        return !IsBusy
            && !string.IsNullOrWhiteSpace(PresentationFilePath);
    }

    [RelayCommand(CanExecute = nameof(CanUsePresentationFile))]
    private async Task CopyPresentationPathAsync()
    {
        if (string.IsNullOrWhiteSpace(PresentationFilePath))
        {
            return;
        }

        try
        {
            await _clipboardService.SetTextAsync(PresentationFilePath);
            StatusMessage = "Copied the PowerPoint file location.";
        }
        catch (Exception exception)
        {
            StatusMessage =
                $"Could not copy the PowerPoint file location: {exception.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanUsePresentationFile))]
    private void OpenPresentationFolder()
    {
        if (string.IsNullOrWhiteSpace(PresentationFilePath))
        {
            return;
        }

        try
        {
            _fileLauncher.OpenContainingFolder(PresentationFilePath);
            StatusMessage = "Opened the folder containing the PowerPoint presentation.";
        }
        catch (Exception exception)
        {
            StatusMessage =
                $"Could not open the PowerPoint folder: {exception.Message}";
        }
    }

    [RelayCommand]
    private void UseExample(string prompt)
    {
        Query = prompt;
    }

    partial void OnQueryChanged(string value)
    {
        GenerateCommand.NotifyCanExecuteChanged();
    }

    partial void OnProjectTitleChanged(string value)
    {
        RenameProjectCommand.NotifyCanExecuteChanged();
    }

    partial void OnHasActiveProjectChanged(bool value)
    {
        OnPropertyChanged(nameof(PrimaryActionText));
        OnPropertyChanged(nameof(QueryPlaceholder));
        RenameProjectCommand.NotifyCanExecuteChanged();
        RequestProjectDeletionCommand.NotifyCanExecuteChanged();
        ConfirmProjectDeletionCommand.NotifyCanExecuteChanged();
        CancelProjectDeletionCommand.NotifyCanExecuteChanged();
        GoHomeCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsDeleteConfirmationVisibleChanged(bool value)
    {
        RequestProjectDeletionCommand.NotifyCanExecuteChanged();
        ConfirmProjectDeletionCommand.NotifyCanExecuteChanged();
        CancelProjectDeletionCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        GenerateCommand.NotifyCanExecuteChanged();
        GitHubSignInCommand.NotifyCanExecuteChanged();
        OpenExternalCommand.NotifyCanExecuteChanged();
        BuildPowerPointCommand.NotifyCanExecuteChanged();
        OpenPresentationCommand.NotifyCanExecuteChanged();
        CopyPresentationPathCommand.NotifyCanExecuteChanged();
        OpenPresentationFolderCommand.NotifyCanExecuteChanged();
        OpenSettingsCommand.NotifyCanExecuteChanged();
        OpenAboutCommand.NotifyCanExecuteChanged();
        GoHomeCommand.NotifyCanExecuteChanged();
        RefreshSampleQueriesCommand.NotifyCanExecuteChanged();
        RenameProjectCommand.NotifyCanExecuteChanged();
        RequestProjectDeletionCommand.NotifyCanExecuteChanged();
        ConfirmProjectDeletionCommand.NotifyCanExecuteChanged();
        CancelProjectDeletionCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsAuthenticatedChanged(bool value)
    {
        GenerateCommand.NotifyCanExecuteChanged();
        BuildPowerPointCommand.NotifyCanExecuteChanged();
    }

    partial void OnPreviewUriChanged(Uri? value)
    {
        OpenExternalCommand.NotifyCanExecuteChanged();
        BuildPowerPointCommand.NotifyCanExecuteChanged();
        GoHomeCommand.NotifyCanExecuteChanged();
    }

    partial void OnPresentationUriChanged(Uri? value)
    {
        OpenPresentationCommand.NotifyCanExecuteChanged();
    }

    partial void OnPresentationFilePathChanged(string? value)
    {
        CopyPresentationPathCommand.NotifyCanExecuteChanged();
        OpenPresentationFolderCommand.NotifyCanExecuteChanged();
    }

    private void ApplyAuthenticationStatus(AuthenticationStatus status)
    {
        IsAuthenticated = status.IsAuthenticated;
        AuthenticationMessage = status.Message;
        GitHubSignInButtonText = status.IsAuthenticated
            ? "Recheck GitHub sign-in"
            : "GitHub sign-in instructions";
    }
}
