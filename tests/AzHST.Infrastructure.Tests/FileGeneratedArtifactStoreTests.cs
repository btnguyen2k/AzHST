namespace AzHST.Infrastructure.Tests;

public sealed class FileGeneratedArtifactStoreTests : IDisposable
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
    public async Task SaveAsync_CreatesArtifactDirectoryAndIndexPage()
    {
        var generatedDirectory = Path.Combine(_testDirectory, "generated");
        var store = CreateStore(generatedDirectory);

        var artifact = await store.SaveAsync(
            "0199abcd1234-azure-app-gateway",
            "<!doctype html><html><body>Gateway</body></html>",
            string.Empty);

        Assert.Equal("0199abcd1234-azure-app-gateway", artifact.Id);
        Assert.Equal(
            Path.Combine(generatedDirectory, artifact.Id),
            artifact.DirectoryPath);
        Assert.Equal(
            Path.Combine(generatedDirectory, artifact.Id, "index.html"),
            artifact.FilePath);
        Assert.True(File.Exists(artifact.FilePath));
        Assert.Equal(
            "<!doctype html><html><body>Gateway</body></html>",
            await File.ReadAllTextAsync(artifact.FilePath));
        Assert.True(artifact.FileUri.IsFile);
    }

    [Fact]
    public async Task SaveAsync_UsesConfiguredOutputAsGeneratedRoot()
    {
        var store = CreateStore(Path.Combine(_testDirectory, "default"));
        var configuredDirectory = Path.Combine(_testDirectory, "configured");

        var artifact = await store.SaveAsync(
            "0199abcd1234-bcdr",
            "<html><head></head><body>BCDR</body></html>",
            configuredDirectory);

        Assert.Equal(
            Path.Combine(configuredDirectory, artifact.Id, "index.html"),
            artifact.FilePath);
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../escape")]
    [InlineData(@"..\escape")]
    [InlineData("nested/id")]
    [InlineData(@"nested\id")]
    public async Task SaveAsync_RejectsUnsafeVisualizationId(string visualizationId)
    {
        var store = CreateStore(Path.Combine(_testDirectory, "generated"));

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.SaveAsync(visualizationId, "<html></html>", string.Empty));
    }

    [Fact]
    public async Task SaveAsync_DoesNotOverwriteExistingIndexPage()
    {
        var store = CreateStore(Path.Combine(_testDirectory, "generated"));
        const string visualizationId = "0199abcd1234-front-door";

        await store.SaveAsync(
            visualizationId,
            "<html><body>First</body></html>",
            string.Empty);

        await Assert.ThrowsAsync<IOException>(
            () => store.SaveAsync(
                visualizationId,
                "<html><body>Second</body></html>",
                string.Empty));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private FileGeneratedArtifactStore CreateStore(string generatedDirectory)
    {
        var paths = new ApplicationPaths(
            _testDirectory,
            Path.Combine(_testDirectory, "settings.json"),
            generatedDirectory,
            Path.Combine(_testDirectory, "copilot"));

        return new FileGeneratedArtifactStore(paths);
    }
}
