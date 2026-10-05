namespace AzHST.Infrastructure.Tests;

public sealed class ApplicationPathsTests
{
    [Fact]
    public void CreatePortable_UsesExecutableAdjacentStorage()
    {
        var applicationDirectory = Path.Combine(
            Path.GetTempPath(),
            "AzHST.Tests",
            Guid.NewGuid().ToString("N"),
            "application");
        var applicationRoot = Path.GetFullPath(applicationDirectory);
        var dataDirectory = Path.Combine(applicationRoot, "data");

        var paths = ApplicationPaths.CreatePortable(applicationDirectory);

        Assert.Equal(dataDirectory, paths.DataDirectory);
        Assert.Equal(
            Path.Combine(dataDirectory, "settings.json"),
            paths.SettingsFile);
        Assert.Equal(
            Path.Combine(applicationRoot, "generated"),
            paths.GeneratedPagesDirectory);
        Assert.Equal(
            Path.Combine(dataDirectory, "copilot"),
            paths.CopilotDirectory);
        Assert.Equal(
            Path.Combine(dataDirectory, "azhst.db"),
            paths.SampleQueryDatabaseFile);
        Assert.Equal(
            Path.Combine(dataDirectory, "projects.db"),
            paths.ProjectDatabaseFile);
    }

    [Fact]
    public void CreatePortable_UsesRuntimeApplicationDirectoryByDefault()
    {
        var applicationRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(AppContext.BaseDirectory));

        var paths = ApplicationPaths.CreatePortable();

        Assert.Equal(
            Path.Combine(applicationRoot, "data"),
            paths.DataDirectory);
        Assert.Equal(
            Path.Combine(applicationRoot, "generated"),
            paths.GeneratedPagesDirectory);
    }
}
