using AzHST.Application.Abstractions;
using AzHST.Application.Exceptions;
using AzHST.Application.Models;
using AzHST.Application.Services;

namespace AzHST.Application.Tests;

public sealed class ProjectUseCaseTests : IDisposable
{
    private static readonly DateTimeOffset FixedTime =
        new(2026, 10, 3, 8, 0, 0, TimeSpan.Zero);

    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "AzHST.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CreateProjectAsync_PersistsRevisionAndConversation()
    {
        var repository = new MemoryProjectRepository();
        var artifacts = new MemoryProjectArtifactStore(_testDirectory);
        var conversation = new StubProjectConversation
        {
            CreateResponse = VisualizationTestData.ValidHtml,
        };
        var useCase = CreateCreateUseCase(
            repository,
            artifacts,
            conversation);

        var workspace = await useCase.ExecuteAsync(
            "  Explain Azure Functions  ",
            new AppSettings
            {
                Model = "test-model",
                OutputDirectory = _testDirectory,
            });

        var expectedId =
            $"{FixedTime.ToUnixTimeMilliseconds():x12}-azure-functions";
        Assert.Equal(expectedId, workspace.Project.Id);
        Assert.Equal("Explain Azure Functions", workspace.Project.Title);
        Assert.Equal(
            $"azhst-project-{expectedId}",
            workspace.Project.CopilotSessionId);
        Assert.Equal(ProjectRevisionIds.Current, workspace.Project.Revision.RevisionId);
        Assert.Null(workspace.Project.Revision.PresentationFilePath);
        Assert.Equal(workspace.Project, await repository.GetAsync(expectedId));
        Assert.Equal(
            workspace.Project.CopilotSessionId,
            conversation.CreatedSessionId);
        Assert.Equal("test-model", conversation.CreatedModel);
        Assert.Equal(
            OutputThemeSettings.DefaultHtmlThemeId,
            conversation.CreatedThemeId);
        Assert.Contains("Content-Security-Policy", workspace.Visualization.Html);
        Assert.Equal(
            Path.Combine(_testDirectory, expectedId, "000", "index.html"),
            workspace.Visualization.FilePath);
    }

    [Fact]
    public async Task CreateProjectAsync_RejectsInvalidAssessmentWithoutConversation()
    {
        var repository = new MemoryProjectRepository();
        var artifacts = new MemoryProjectArtifactStore(_testDirectory);
        var conversation = new StubProjectConversation
        {
            CreateResponse = VisualizationTestData.ValidHtml,
        };
        var client = new StubCopilotClient
        {
            Assessment = new VisualizationQueryAssessment
            {
                IsValid = false,
                Message = "Ask an Azure question that can be visualized.",
            },
        };
        var useCase = CreateCreateUseCase(
            repository,
            artifacts,
            conversation,
            client);

        var exception =
            await Assert.ThrowsAsync<InvalidVisualizationQueryException>(
                () => useCase.ExecuteAsync(
                    "Write a poem",
                    new AppSettings()));

        Assert.Equal(
            "Ask an Azure question that can be visualized.",
            exception.Message);
        Assert.Null(conversation.CreatedSessionId);
        Assert.Empty(await repository.ListAsync());
        Assert.Equal(0, artifacts.SaveCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateProjectAsync_RejectsBlankQuestion(string question)
    {
        var repository = new MemoryProjectRepository();
        var artifacts = new MemoryProjectArtifactStore(_testDirectory);
        var conversation = new StubProjectConversation();
        var useCase = CreateCreateUseCase(
            repository,
            artifacts,
            conversation);

        await Assert.ThrowsAsync<VisualizationGenerationException>(
            () => useCase.ExecuteAsync(question, new AppSettings()));

        Assert.Null(conversation.CreatedSessionId);
    }

    [Fact]
    public async Task CreateProjectAsync_RejectsOversizedQuestion()
    {
        var repository = new MemoryProjectRepository();
        var artifacts = new MemoryProjectArtifactStore(_testDirectory);
        var conversation = new StubProjectConversation();
        var useCase = CreateCreateUseCase(
            repository,
            artifacts,
            conversation);
        var question = new string(
            'x',
            CreateProjectUseCase.MaximumQueryLength + 1);

        await Assert.ThrowsAsync<VisualizationGenerationException>(
            () => useCase.ExecuteAsync(question, new AppSettings()));

        Assert.Null(conversation.CreatedSessionId);
    }

    [Fact]
    public async Task CreateProjectAsync_InvalidHtmlDeletesPartialConversation()
    {
        var repository = new MemoryProjectRepository();
        var artifacts = new MemoryProjectArtifactStore(_testDirectory);
        var conversation = new StubProjectConversation
        {
            CreateResponse = "<html><body>Incomplete</body></html>",
        };
        var useCase = CreateCreateUseCase(
            repository,
            artifacts,
            conversation);

        await Assert.ThrowsAnyAsync<Exception>(
            () => useCase.ExecuteAsync(
                "Explain Azure Functions",
                new AppSettings()));

        Assert.Equal(
            conversation.CreatedSessionId,
            conversation.DeletedSessionId);
        Assert.Empty(await repository.ListAsync());
        Assert.Equal(0, artifacts.SaveCount);
    }

    [Fact]
    public async Task RefineProjectAsync_ResumesSessionAndReplacesRevision()
    {
        var repository = new MemoryProjectRepository();
        var artifacts = new MemoryProjectArtifactStore(_testDirectory);
        var conversation = new StubProjectConversation
        {
            CreateResponse = VisualizationTestData.ValidHtml,
            RefineResponse = VisualizationTestData.ValidHtml.Replace(
                "Azure Functions",
                "Refined Azure Functions",
                StringComparison.Ordinal),
        };
        var createUseCase = CreateCreateUseCase(
            repository,
            artifacts,
            conversation);
        var original = await createUseCase.ExecuteAsync(
            "Explain Azure Functions",
            new AppSettings { OutputDirectory = _testDirectory });
        await repository.SaveAsync(original.Project with
        {
            Revision = original.Project.Revision with
            {
                PresentationFilePath = Path.Combine(
                    original.Visualization.DirectoryPath,
                    "presentation.pptx"),
            },
        });
        var refineUseCase = new RefineProjectUseCase(
            conversation,
            new GeneratedHtmlDocumentProcessor(),
            artifacts,
            repository,
            new StubOutputThemeCatalog(),
            new FixedTimeProvider(FixedTime.AddMinutes(5)));

        var updated = await refineUseCase.ExecuteAsync(
            original.Project.Id,
            "Emphasize scaling and monitoring",
            new AppSettings
            {
                Model = "updated-model",
                Themes = new OutputThemeSettings
                {
                    HtmlThemeId = "custom-night",
                    PresentationThemeId = "custom-presentation",
                },
            });

        Assert.Equal(
            original.Project.CopilotSessionId,
            conversation.RefinedSessionId);
        Assert.Equal(
            "Emphasize scaling and monitoring",
            conversation.FollowUpRequest);
        Assert.Equal("updated-model", updated.Project.SelectedModelId);
        Assert.Equal("custom-night", updated.Project.HtmlThemeId);
        Assert.Equal(
            "custom-presentation",
            updated.Project.PresentationThemeId);
        Assert.Null(updated.Project.Revision.PresentationFilePath);
        Assert.True(artifacts.LastOverwrite);
        Assert.Contains("Refined Azure Functions", updated.Visualization.Html);
    }

    [Fact]
    public async Task RefineProjectAsync_InvalidHtmlLeavesCurrentRevisionUnchanged()
    {
        var repository = new MemoryProjectRepository();
        var artifacts = new MemoryProjectArtifactStore(_testDirectory);
        var conversation = new StubProjectConversation
        {
            CreateResponse = VisualizationTestData.ValidHtml,
            RefineResponse = "<html><body>Incomplete</body></html>",
        };
        var createUseCase = CreateCreateUseCase(
            repository,
            artifacts,
            conversation);
        var original = await createUseCase.ExecuteAsync(
            "Explain Azure Functions",
            new AppSettings { OutputDirectory = _testDirectory });
        var originalHtml = original.Visualization.Html;
        var refineUseCase = new RefineProjectUseCase(
            conversation,
            new GeneratedHtmlDocumentProcessor(),
            artifacts,
            repository,
            new StubOutputThemeCatalog());

        await Assert.ThrowsAnyAsync<Exception>(
            () => refineUseCase.ExecuteAsync(
                original.Project.Id,
                "Remove everything important",
                new AppSettings()));

        Assert.Equal(
            original.Project,
            await repository.GetAsync(original.Project.Id));
        Assert.Equal(
            originalHtml,
            (await artifacts.LoadAsync(original.Project)).Html);
        Assert.False(artifacts.LastOverwrite);
    }

    [Fact]
    public async Task RefineProjectAsync_RepositoryFailureRestoresPreviousHtml()
    {
        var repository = new MemoryProjectRepository();
        var artifacts = new MemoryProjectArtifactStore(_testDirectory);
        var conversation = new StubProjectConversation
        {
            CreateResponse = VisualizationTestData.ValidHtml,
            RefineResponse = VisualizationTestData.ValidHtml.Replace(
                "Azure Functions",
                "Updated Functions",
                StringComparison.Ordinal),
        };
        var createUseCase = CreateCreateUseCase(
            repository,
            artifacts,
            conversation);
        var original = await createUseCase.ExecuteAsync(
            "Explain Azure Functions",
            new AppSettings { OutputDirectory = _testDirectory });
        repository.FailNextSave = true;
        var refineUseCase = new RefineProjectUseCase(
            conversation,
            new GeneratedHtmlDocumentProcessor(),
            artifacts,
            repository,
            new StubOutputThemeCatalog());

        await Assert.ThrowsAsync<IOException>(
            () => refineUseCase.ExecuteAsync(
                original.Project.Id,
                "Update the design",
                new AppSettings()));

        Assert.Equal(
            original.Visualization.Html,
            (await artifacts.LoadAsync(original.Project)).Html);
        Assert.Equal(
            original.Project,
            await repository.GetAsync(original.Project.Id));
    }

    [Fact]
    public async Task ProjectLifecycleUseCases_OpenRenameRecordAndDelete()
    {
        var repository = new MemoryProjectRepository();
        var artifacts = new MemoryProjectArtifactStore(_testDirectory);
        var conversation = new StubProjectConversation
        {
            CreateResponse = VisualizationTestData.ValidHtml,
        };
        var created = await CreateCreateUseCase(
                repository,
                artifacts,
                conversation)
            .ExecuteAsync(
                "Explain Azure Functions",
                new AppSettings { OutputDirectory = _testDirectory });

        var opened = await new OpenProjectUseCase(repository, artifacts)
            .ExecuteAsync(created.Project.Id);
        Assert.Equal(created.Project, opened.Project);
        Assert.Equal(created.Visualization.Html, opened.Visualization.Html);

        var renamed = await new RenameProjectUseCase(
                repository,
                new FixedTimeProvider(FixedTime.AddMinutes(1)))
            .ExecuteAsync(created.Project.Id, "Functions architecture");
        Assert.Equal("Functions architecture", renamed.Title);

        var presentation = new PresentationArtifact(
            created.Project.Id,
            Path.Combine(created.Visualization.DirectoryPath, "presentation.pptx"),
            new Uri("file:///presentation.pptx"),
            6);
        var recorded = await new RecordProjectPresentationUseCase(
                repository,
                new FixedTimeProvider(FixedTime.AddMinutes(2)))
            .ExecuteAsync(
                created.Project.Id,
                presentation,
                "professional-night");
        Assert.Equal(
            presentation.FilePath,
            recorded.Revision.PresentationFilePath);

        await new DeleteProjectUseCase(
                repository,
                artifacts,
                conversation)
            .ExecuteAsync(created.Project.Id);

        Assert.Null(await repository.GetAsync(created.Project.Id));
        Assert.Equal(
            created.Project.CopilotSessionId,
            conversation.DeletedSessionId);
        Assert.True(artifacts.DeleteCalled);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private static CreateProjectUseCase CreateCreateUseCase(
        MemoryProjectRepository repository,
        MemoryProjectArtifactStore artifacts,
        StubProjectConversation conversation,
        StubCopilotClient? client = null)
    {
        return new CreateProjectUseCase(
            client ?? new StubCopilotClient(),
            conversation,
            new GeneratedHtmlDocumentProcessor(),
            artifacts,
            repository,
            new VisualizationArtifactIdGenerator(
                new FixedTimeProvider(FixedTime)),
            new StubOutputThemeCatalog(),
            new FixedTimeProvider(FixedTime));
    }

    private sealed class StubCopilotClient : ICopilotVisualizationClient
    {
        public VisualizationQueryAssessment Assessment { get; init; } =
            new()
            {
                IsValid = true,
                Message = "Valid.",
                SuggestedSlug = "azure-functions",
            };

        public Task<VisualizationQueryAssessment> AssessQueryAsync(
            string query,
            string model,
            IProgress<GenerationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Assessment);
        }

    }

    private sealed class StubProjectConversation : ICopilotProjectConversation
    {
        public string CreateResponse { get; init; } = string.Empty;

        public string RefineResponse { get; init; } = string.Empty;

        public string? CreatedSessionId { get; private set; }

        public string? CreatedModel { get; private set; }

        public string? CreatedThemeId { get; private set; }

        public string? RefinedSessionId { get; private set; }

        public string? FollowUpRequest { get; private set; }

        public string? DeletedSessionId { get; private set; }

        public Task<string> CreateAsync(
            string sessionId,
            string query,
            string model,
            string visualizationId,
            HtmlThemeDefinition theme,
            IProgress<GenerationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            CreatedSessionId = sessionId;
            CreatedModel = model;
            CreatedThemeId = theme.Id;
            return Task.FromResult(CreateResponse);
        }

        public Task<string> RefineAsync(
            string sessionId,
            string originalQuery,
            string followUpRequest,
            string model,
            string visualizationId,
            HtmlThemeDefinition theme,
            IProgress<GenerationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            RefinedSessionId = sessionId;
            FollowUpRequest = followUpRequest;
            return Task.FromResult(RefineResponse);
        }

        public Task DeleteAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            DeletedSessionId = sessionId;
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryProjectRepository : IProjectRepository
    {
        private readonly Dictionary<string, Project> _projects =
            new(StringComparer.Ordinal);

        public bool FailNextSave { get; set; }

        public Task InitializeAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Project>> ListAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Project>>(
                _projects.Values
                    .OrderByDescending(project => project.UpdatedUtc)
                    .ToArray());
        }

        public Task<Project?> GetAsync(
            string projectId,
            CancellationToken cancellationToken = default)
        {
            _projects.TryGetValue(projectId, out var project);
            return Task.FromResult(project);
        }

        public Task SaveAsync(
            Project project,
            CancellationToken cancellationToken = default)
        {
            if (FailNextSave)
            {
                FailNextSave = false;
                throw new IOException("Simulated project database failure.");
            }

            _projects[project.Id] = project;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            string projectId,
            CancellationToken cancellationToken = default)
        {
            _projects.Remove(projectId);
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryProjectArtifactStore(string defaultRoot)
        : IProjectArtifactStore
    {
        private readonly Dictionary<string, VisualizationArtifact> _artifacts =
            new(StringComparer.Ordinal);

        public bool LastOverwrite { get; private set; }

        public bool DeleteCalled { get; private set; }

        public int SaveCount { get; private set; }

        public Task<VisualizationArtifact> SaveAsync(
            string projectId,
            string revisionId,
            string html,
            string outputDirectory,
            bool overwrite,
            CancellationToken cancellationToken = default)
        {
            LastOverwrite = overwrite;
            SaveCount++;
            var root = string.IsNullOrWhiteSpace(outputDirectory)
                ? defaultRoot
                : outputDirectory;
            var directory = Path.Combine(root, projectId, revisionId);
            var filePath = Path.Combine(directory, "index.html");
            var artifact = new VisualizationArtifact(
                projectId,
                directory,
                filePath,
                new Uri(Path.GetFullPath(filePath)))
            {
                Html = html,
            };
            _artifacts[projectId] = artifact;
            return Task.FromResult(artifact);
        }

        public Task<VisualizationArtifact> LoadAsync(
            Project project,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_artifacts[project.Id]);
        }

        public Task DeleteAsync(
            Project project,
            CancellationToken cancellationToken = default)
        {
            DeleteCalled = true;
            _artifacts.Remove(project.Id);
            return Task.CompletedTask;
        }
    }

    private sealed class StubOutputThemeCatalog : IOutputThemeCatalog
    {
        public IReadOnlyList<OutputThemeOption> HtmlThemes { get; } = [];

        public IReadOnlyList<OutputThemeOption> PresentationThemes { get; } = [];

        public HtmlThemeDefinition GetHtmlTheme(string id)
        {
            return VisualizationTestData.CreateHtmlTheme(id);
        }

        public PresentationThemeDefinition GetPresentationTheme(string id)
        {
            throw new NotSupportedException();
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
