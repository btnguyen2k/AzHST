using AzHST.Application.Models;

namespace AzHST.Infrastructure.Tests;

public sealed class FileProjectArtifactStoreTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "AzHST.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void CreateDefault_UsesGeneratedDirectoryUnderWorkingDirectory()
    {
        var paths = ApplicationPaths.CreateDefault();

        Assert.Equal(
            Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "generated")),
            paths.GeneratedPagesDirectory);
    }

    [Fact]
    public void CreateDefault_SeparatesSampleAndProjectDatabases()
    {
        var paths = ApplicationPaths.CreateDefault();

        Assert.Equal(
            Path.GetFullPath(
                Path.Combine(Environment.CurrentDirectory, "data", "azhst.db")),
            paths.SampleQueryDatabaseFile);
        Assert.Equal(
            Path.Combine(paths.DataDirectory, "projects.db"),
            paths.ProjectDatabaseFile);
        Assert.NotEqual(
            paths.SampleQueryDatabaseFile,
            paths.ProjectDatabaseFile);
    }

    [Fact]
    public async Task SaveAsync_CreatesFixedRevisionDirectory()
    {
        var root = Path.Combine(_testDirectory, "generated");
        var store = CreateStore(root);

        var artifact = await store.SaveAsync(
            "project-id",
            ProjectRevisionIds.Current,
            "<html><body>Initial</body></html>",
            string.Empty,
            overwrite: false);

        Assert.Equal("project-id", artifact.Id);
        Assert.Equal(
            Path.Combine(root, "project-id", "000"),
            artifact.DirectoryPath);
        Assert.Equal(
            Path.Combine(root, "project-id", "000", "index.html"),
            artifact.FilePath);
        Assert.Equal(
            "<html><body>Initial</body></html>",
            await File.ReadAllTextAsync(artifact.FilePath));
    }

    [Fact]
    public async Task SaveAsync_ReplacesRevisionOnlyWhenRequested()
    {
        var root = Path.Combine(_testDirectory, "generated");
        var store = CreateStore(root);

        await store.SaveAsync(
            "project-id",
            ProjectRevisionIds.Current,
            "<html><body>Initial</body></html>",
            string.Empty,
            overwrite: false);

        await Assert.ThrowsAsync<IOException>(
            () => store.SaveAsync(
                "project-id",
                ProjectRevisionIds.Current,
                "<html><body>Rejected</body></html>",
                string.Empty,
                overwrite: false));

        var artifact = await store.SaveAsync(
            "project-id",
            ProjectRevisionIds.Current,
            "<html><body>Updated</body></html>",
            string.Empty,
            overwrite: true);

        Assert.Equal(
            "<html><body>Updated</body></html>",
            await File.ReadAllTextAsync(artifact.FilePath));
        Assert.Empty(
            Directory.EnumerateFiles(
                artifact.DirectoryPath,
                "*.tmp",
                SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public async Task LoadAndDeleteAsync_UsePersistedRevisionPath()
    {
        var configuredRoot = Path.Combine(_testDirectory, "custom-output");
        var store = CreateStore(Path.Combine(_testDirectory, "default"));
        var artifact = await store.SaveAsync(
            "project-id",
            ProjectRevisionIds.Current,
            "<html><body>Saved</body></html>",
            configuredRoot,
            overwrite: false);
        var project = CreateProject(artifact.FilePath);

        var loaded = await store.LoadAsync(project);

        Assert.Equal(artifact.FilePath, loaded.FilePath);
        Assert.Equal("<html><body>Saved</body></html>", loaded.Html);

        await store.DeleteAsync(project);

        Assert.False(Directory.Exists(
            Path.Combine(configuredRoot, "project-id")));
    }

    [Fact]
    public async Task LoadAsync_RejectsPathThatDoesNotMatchProjectIdentity()
    {
        var store = CreateStore(Path.Combine(_testDirectory, "generated"));
        var project = CreateProject(Path.Combine(
            _testDirectory,
            "different-project",
            "000",
            "index.html"));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => store.LoadAsync(project));
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../escape")]
    [InlineData(@"..\escape")]
    [InlineData("nested/id")]
    [InlineData(@"nested\id")]
    [InlineData(@"C:\escape")]
    [InlineData("bad:name")]
    [InlineData("bad?name")]
    [InlineData("trailing.")]
    [InlineData("trailing ")]
    public async Task SaveAsync_RejectsUnsafeProjectId(string projectId)
    {
        var store = CreateStore(Path.Combine(_testDirectory, "generated"));

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.SaveAsync(
                projectId,
                ProjectRevisionIds.Current,
                "<html></html>",
                string.Empty,
                overwrite: false));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private FileProjectArtifactStore CreateStore(string generatedDirectory)
    {
        var paths = new ApplicationPaths(
            _testDirectory,
            Path.Combine(_testDirectory, "settings.json"),
            generatedDirectory,
            Path.Combine(_testDirectory, "copilot"),
            Path.Combine(_testDirectory, "data", "azhst.db"));
        return new FileProjectArtifactStore(paths);
    }

    private static Project CreateProject(string htmlFilePath)
    {
        var now = DateTimeOffset.UtcNow;
        return new Project(
            "project-id",
            "Project",
            "Question",
            "session-id",
            now,
            now,
            CopilotModelSelection.Automatic,
            OutputThemeSettings.DefaultHtmlThemeId,
            OutputThemeSettings.DefaultPresentationThemeId,
            new ProjectRevision(
                "project-id",
                ProjectRevisionIds.Current,
                htmlFilePath,
                null,
                ProjectGenerationStatus.Ready,
                now));
    }
}
