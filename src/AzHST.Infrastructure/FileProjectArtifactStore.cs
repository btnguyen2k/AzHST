using AzHST.Application.Abstractions;
using AzHST.Application.Models;

namespace AzHST.Infrastructure;

public sealed class FileProjectArtifactStore : IProjectArtifactStore
{
    private readonly ApplicationPaths _paths;

    public FileProjectArtifactStore(ApplicationPaths paths)
    {
        _paths = paths;
    }

    public async Task<VisualizationArtifact> SaveAsync(
        string projectId,
        string revisionId,
        string html,
        string outputDirectory,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        ValidatePathSegment(projectId, nameof(projectId));
        ValidatePathSegment(revisionId, nameof(revisionId));
        if (string.IsNullOrWhiteSpace(html))
        {
            throw new ArgumentException(
                "The generated HTML cannot be empty.",
                nameof(html));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var rootDirectory = string.IsNullOrWhiteSpace(outputDirectory)
            ? _paths.GeneratedPagesDirectory
            : Path.GetFullPath(outputDirectory);
        var revisionDirectory = Path.Combine(
            rootDirectory,
            projectId,
            revisionId);
        var htmlFile = Path.Combine(revisionDirectory, "index.html");
        var temporaryFile = Path.Combine(
            revisionDirectory,
            $".index-{Guid.NewGuid():N}.tmp");

        Directory.CreateDirectory(revisionDirectory);

        try
        {
            await File.WriteAllTextAsync(
                temporaryFile,
                html,
                cancellationToken);
            File.Move(temporaryFile, htmlFile, overwrite);
        }
        finally
        {
            if (File.Exists(temporaryFile))
            {
                File.Delete(temporaryFile);
            }
        }

        return new VisualizationArtifact(
            projectId,
            revisionDirectory,
            htmlFile,
            new Uri(htmlFile))
        {
            Html = html,
        };
    }

    public async Task<VisualizationArtifact> LoadAsync(
        Project project,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        var (htmlFile, revisionDirectory, _) =
            ValidateArtifactPath(project);
        if (!File.Exists(htmlFile))
        {
            throw new FileNotFoundException(
                "The project's generated HTML file is missing.",
                htmlFile);
        }

        var html = await File.ReadAllTextAsync(htmlFile, cancellationToken);
        if (string.IsNullOrWhiteSpace(html))
        {
            throw new InvalidDataException(
                "The project's generated HTML file is empty.");
        }

        return new VisualizationArtifact(
            project.Id,
            revisionDirectory,
            htmlFile,
            new Uri(htmlFile))
        {
            Html = html,
        };
    }

    public Task DeleteAsync(
        Project project,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(project.Revision.HtmlFilePath))
        {
            return Task.CompletedTask;
        }

        var (_, _, projectDirectory) = ValidateArtifactPath(project);

        if (Directory.Exists(projectDirectory))
        {
            Directory.Delete(projectDirectory, recursive: true);
        }

        return Task.CompletedTask;
    }

    private static (
        string HtmlFile,
        string RevisionDirectory,
        string ProjectDirectory) ValidateArtifactPath(Project project)
    {
        ValidatePathSegment(project.Id, nameof(project));
        ValidatePathSegment(
            project.Revision.RevisionId,
            nameof(project));

        if (string.IsNullOrWhiteSpace(project.Revision.HtmlFilePath))
        {
            throw new InvalidDataException(
                "The project HTML path is missing.");
        }

        var htmlFile = Path.GetFullPath(project.Revision.HtmlFilePath);
        var revisionDirectory = Path.GetDirectoryName(htmlFile)
            ?? throw new InvalidDataException(
                "The project HTML path has no revision directory.");
        var projectDirectory = Path.GetDirectoryName(revisionDirectory)
            ?? throw new InvalidDataException(
                "The project HTML path has no project directory.");

        if (!string.Equals(
                Path.GetFileName(htmlFile),
                "index.html",
                PathComparison)
            || !string.Equals(
                Path.GetFileName(revisionDirectory),
                project.Revision.RevisionId,
                PathComparison)
            || !string.Equals(
                Path.GetFileName(projectDirectory),
                project.Id,
                PathComparison))
        {
            throw new InvalidDataException(
                "The project HTML path does not match its project and revision IDs.");
        }

        return (htmlFile, revisionDirectory, projectDirectory);
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static void ValidatePathSegment(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value is "." or ".."
            || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || value.Contains(Path.DirectorySeparatorChar)
            || value.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException(
                "The value must be a safe single path segment.",
                parameterName);
        }
    }
}
