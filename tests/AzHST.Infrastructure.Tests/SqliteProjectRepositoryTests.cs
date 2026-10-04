using AzHST.Application.Models;

namespace AzHST.Infrastructure.Tests;

public sealed class SqliteProjectRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset CreatedUtc =
        new(2026, 10, 2, 10, 30, 0, TimeSpan.Zero);

    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "AzHST.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task InitializeAsync_CreatesDedicatedProjectDatabase()
    {
        var paths = CreatePaths();
        var repository = new SqliteProjectRepository(paths);

        await repository.InitializeAsync();
        await repository.InitializeAsync();

        Assert.True(File.Exists(paths.ProjectDatabaseFile));
        Assert.NotEqual(
            paths.SampleQueryDatabaseFile,
            paths.ProjectDatabaseFile);
        Assert.Empty(await repository.ListAsync());
    }

    [Fact]
    public async Task SaveAsync_PersistsAndUpdatesCurrentRevision()
    {
        var repository = new SqliteProjectRepository(CreatePaths());
        var project = CreateProject();

        await repository.SaveAsync(project);

        var loaded = await repository.GetAsync(project.Id);
        var listed = Assert.Single(await repository.ListAsync());
        Assert.Equal(project, loaded);
        Assert.Equal(project, listed);

        var updated = project with
        {
            Title = "Renamed project",
            UpdatedUtc = CreatedUtc.AddMinutes(10),
            Revision = project.Revision with
            {
                PresentationFilePath = @"C:\output\presentation.pptx",
                UpdatedUtc = CreatedUtc.AddMinutes(10),
            },
        };

        await repository.SaveAsync(updated);

        Assert.Equal(updated, await repository.GetAsync(project.Id));
    }

    [Fact]
    public async Task DeleteAsync_RemovesProjectAndRevision()
    {
        var repository = new SqliteProjectRepository(CreatePaths());
        var project = CreateProject();
        await repository.SaveAsync(project);

        await repository.DeleteAsync(project.Id);

        Assert.Null(await repository.GetAsync(project.Id));
        Assert.Empty(await repository.ListAsync());
    }

    [Fact]
    public async Task InitializeAsync_DoesNotResetInvalidProjectDatabase()
    {
        var paths = CreatePaths();
        Directory.CreateDirectory(
            Path.GetDirectoryName(paths.ProjectDatabaseFile)!);
        await File.WriteAllTextAsync(
            paths.ProjectDatabaseFile,
            "not a sqlite database");
        var repository = new SqliteProjectRepository(paths);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => repository.InitializeAsync());

        Assert.Contains("was not reset", exception.Message);
        Assert.Equal(
            "not a sqlite database",
            await File.ReadAllTextAsync(paths.ProjectDatabaseFile));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private ApplicationPaths CreatePaths()
    {
        var localData = Path.Combine(_testDirectory, "local");
        return new ApplicationPaths(
            localData,
            Path.Combine(localData, "settings.json"),
            Path.Combine(_testDirectory, "generated"),
            Path.Combine(localData, "copilot"),
            Path.Combine(_testDirectory, "data", "azhst.db"))
        {
            ProjectDatabaseFile = Path.Combine(
                localData,
                "projects.db"),
        };
    }

    private static Project CreateProject()
    {
        return new Project(
            "project-id",
            "Azure Functions",
            "Explain Azure Functions",
            "session-id",
            CreatedUtc,
            CreatedUtc,
            CopilotModelSelection.Automatic,
            OutputThemeSettings.DefaultHtmlThemeId,
            OutputThemeSettings.DefaultPresentationThemeId,
            new ProjectRevision(
                "project-id",
                ProjectRevisionIds.Current,
                @"C:\output\project-id\000\index.html",
                null,
                ProjectGenerationStatus.Ready,
                CreatedUtc));
    }
}
