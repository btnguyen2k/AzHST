using System.Text;
using AzHST.Application.Abstractions;
using AzHST.Application.Models;

namespace AzHST.Infrastructure;

public sealed class FileGeneratedArtifactStore : IGeneratedArtifactStore
{
    private readonly string _defaultOutputDirectory;

    public FileGeneratedArtifactStore(ApplicationPaths paths)
    {
        _defaultOutputDirectory = paths.GeneratedPagesDirectory;
    }

    public async Task<VisualizationArtifact> SaveAsync(
        string visualizationId,
        string html,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ValidateVisualizationId(visualizationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(html);

        var rootDirectory = string.IsNullOrWhiteSpace(outputDirectory)
            ? _defaultOutputDirectory
            : outputDirectory.Trim();

        rootDirectory = Path.GetFullPath(rootDirectory);
        var artifactDirectory = Path.Combine(rootDirectory, visualizationId);
        var filePath = Path.Combine(artifactDirectory, "index.html");
        var temporaryFile = Path.Combine(
            artifactDirectory,
            $".index-{Guid.NewGuid():N}.tmp");

        Directory.CreateDirectory(artifactDirectory);

        try
        {
            await File.WriteAllTextAsync(
                temporaryFile,
                html,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);

            File.Move(temporaryFile, filePath, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporaryFile))
            {
                File.Delete(temporaryFile);
            }
        }

        return new VisualizationArtifact(
            visualizationId,
            artifactDirectory,
            filePath,
            new Uri(filePath, UriKind.Absolute))
        {
            Html = html,
        };
    }

    private static void ValidateVisualizationId(string visualizationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(visualizationId);

        if (visualizationId is "." or ".."
            || visualizationId.Contains('/')
            || visualizationId.Contains('\\')
            || !string.Equals(
                visualizationId,
                Path.GetFileName(visualizationId),
                StringComparison.Ordinal)
            || visualizationId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException(
                "The visualization ID must be a single valid directory name.",
                nameof(visualizationId));
        }
    }
}
