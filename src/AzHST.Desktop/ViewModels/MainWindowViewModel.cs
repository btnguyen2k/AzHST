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
    private readonly GenerateVisualizationUseCase _generationUseCase;
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
    private readonly WebViewAvailability _webViewAvailability;
    private readonly bool _skipGitHubSignInAtStartup;
    private VisualizationArtifact? _visualizationArtifact;
    private string _generatedQuery = string.Empty;
    private bool _sampleQueryStoreInitialized;
    private bool _initialized;

    [ObservableProperty]
    private string _query = string.Empty;

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
    [NotifyPropertyChangedFor(nameof(HtmlThemeName))]
    [NotifyPropertyChangedFor(nameof(PresentationThemeName))]
    [NotifyPropertyChangedFor(nameof(ResultOpeningMode))]
    private AppSettings _settings = new();

    public MainWindowViewModel(
        GenerateVisualizationUseCase generationUseCase,
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
        ApplicationIdentity applicationIdentity,
        WebViewAvailability webViewAvailability,
        bool skipGitHubSignInAtStartup = false)
    {
        _generationUseCase = generationUseCase;
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

    public ObservableCollection<SampleQueryOptionViewModel> SampleQueries { get; } = [];

    public Uri? EmbeddedPreviewUri =>
        _webViewAvailability.IsAvailable ? PreviewUri : null;

    public string WebViewAvailabilityMessage => _webViewAvailability.Message;

    public string ApplicationIdentityText { get; }

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
                sampleQueryNotice);
            IsBusy = false;
        }
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
        var submittedQuery = Query;
        var progress = new Progress<GenerationProgress>(
            update => StatusMessage = update.Message);

        try
        {
            var artifact = await _generationUseCase.ExecuteAsync(
                submittedQuery,
                Settings,
                progress,
                cancellationToken);

            _visualizationArtifact = artifact;
            _generatedQuery = submittedQuery.Trim();
            GeneratedFilePath = artifact.FilePath;
            PreviewUri = artifact.FileUri;
            PresentationFilePath = null;
            PresentationUri = null;
            BuildPowerPointCommand.NotifyCanExecuteChanged();

            if (Settings.OpenResultsInExternalBrowser || !_webViewAvailability.IsAvailable)
            {
                _browser.Open(artifact.FileUri);
                StatusMessage = _webViewAvailability.IsAvailable
                    ? "Visualization ready and opened in the default browser."
                    : "Embedded preview is unavailable; opened the visualization in the default browser.";
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Generation cancelled.";
        }
        catch (InvalidVisualizationQueryException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (Exception exception)
        {
            StatusMessage = $"Generation failed: {exception.Message}";
        }
        finally
        {
            IsGenerating = false;
            IsBusy = false;
        }
    }

    private bool CanBuildPowerPoint()
    {
        return !IsBusy
            && IsAuthenticated
            && _visualizationArtifact is not null
            && _generatedQuery.Length > 0;
    }

    [RelayCommand(CanExecute = nameof(CanBuildPowerPoint), IncludeCancelCommand = true)]
    private async Task BuildPowerPointAsync(CancellationToken cancellationToken)
    {
        if (_visualizationArtifact is null || _generatedQuery.Length == 0)
        {
            return;
        }

        IsBusy = true;
        IsBuildingPresentation = true;
        var progress = new Progress<GenerationProgress>(
            update => StatusMessage = update.Message);

        try
        {
            var artifact = await _presentationUseCase.ExecuteAsync(
                _generatedQuery,
                _visualizationArtifact,
                Settings,
                progress,
                cancellationToken);

            PresentationFilePath = artifact.FilePath;
            PresentationUri = artifact.FileUri;
            StatusMessage =
                $"PowerPoint ready with {artifact.SlideCount} slides. Copy its path or open its folder below.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "PowerPoint generation cancelled.";
        }
        catch (Exception exception)
        {
            StatusMessage =
                $"PowerPoint generation failed: {exception.Message}";
        }
        finally
        {
            IsBuildingPresentation = false;
            IsBusy = false;
        }
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

    private bool CanGoHome()
    {
        return !IsBusy && HasPreview;
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
                "Home ready. Choose a sample or ask your own Azure question.",
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
        _visualizationArtifact = null;
        _generatedQuery = string.Empty;
        GeneratedFilePath = null;
        PreviewUri = null;
        PresentationFilePath = null;
        PresentationUri = null;
        BuildPowerPointCommand.NotifyCanExecuteChanged();
    }

    private bool CanOpenExternal()
    {
        return !IsBusy && PreviewUri is not null;
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
        GoHomeCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsAuthenticatedChanged(bool value)
    {
        GenerateCommand.NotifyCanExecuteChanged();
        BuildPowerPointCommand.NotifyCanExecuteChanged();
    }

    partial void OnPreviewUriChanged(Uri? value)
    {
        OpenExternalCommand.NotifyCanExecuteChanged();
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
