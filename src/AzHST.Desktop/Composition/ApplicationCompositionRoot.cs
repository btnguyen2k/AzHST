using AzHST.Application.Services;
using AzHST.Desktop.Models;
using AzHST.Desktop.Services;
using AzHST.Desktop.ViewModels;
using AzHST.Desktop.Views;
using AzHST.Infrastructure;

namespace AzHST.Desktop.Composition;

internal static class ApplicationCompositionRoot
{
    public static MainWindow CreateMainWindow()
    {
        var testOptions = DebugTestOptions.FromEnvironment();
        var paths = ApplicationPaths.CreateDefault();
        var settingsRepository = new JsonSettingsRepository(paths);
        var authenticationService = new GitHubCliAuthenticationService(
            testOptions.SimulateMissingGitHubCli);
        var browser = new ExternalBrowserLauncher();
        var artifactStore = new FileGeneratedArtifactStore(paths);
        var themes = FileOutputThemeCatalog.CreateDefault();
        var azureIcons = FileAzureIconCatalog.CreateDefault();
        var copilotClient = new CopilotVisualizationClient(paths, azureIcons);
        var documentProcessor = new GeneratedHtmlDocumentProcessor(azureIcons);
        var artifactIdGenerator = new VisualizationArtifactIdGenerator();
        var webViewAvailability = WebViewAvailability.Detect();
        var applicationIdentity = ApplicationIdentity.FromAssembly(
            typeof(App).Assembly);
        var generationUseCase = new GenerateVisualizationUseCase(
            copilotClient,
            documentProcessor,
            artifactStore,
            artifactIdGenerator,
            themes);
        var sampleQueryRepository = new SqliteSampleQueryRepository(paths);
        var sampleQueryUseCase = new SampleQueryUseCase(
            sampleQueryRepository,
            copilotClient);
        var presentationBuilder = new OpenXmlPresentationBuilder(azureIcons);
        var presentationUseCase = new GeneratePresentationUseCase(
            copilotClient,
            presentationBuilder,
            azureIcons,
            themes);

        MainWindow? mainWindow = null;
        var clipboardService = new ClipboardService(() => mainWindow);
        var settingsDialogService = new SettingsDialogService(
            () => mainWindow,
            themes);
        var loginDialogService = new GitHubLoginDialogService(() => mainWindow);
        var aboutDialogService = new AboutDialogService(
            () => mainWindow,
            new EmbeddedMarkdownDocumentLoader(),
            typeof(App).Assembly,
            applicationIdentity,
            webViewAvailability,
            browser);
        var viewModel = new MainWindowViewModel(
            generationUseCase,
            presentationUseCase,
            sampleQueryUseCase,
            authenticationService,
            settingsRepository,
            themes,
            browser,
            browser,
            clipboardService,
            settingsDialogService,
            loginDialogService,
            aboutDialogService,
            applicationIdentity,
            webViewAvailability,
            testOptions.SkipGitHubSignInAtStartup
                && !testOptions.SimulateMissingGitHubCli);

        mainWindow = new MainWindow
        {
            DataContext = viewModel,
        };

        mainWindow.Opened += async (_, _) => await viewModel.InitializeAsync();
        return mainWindow;
    }
}
