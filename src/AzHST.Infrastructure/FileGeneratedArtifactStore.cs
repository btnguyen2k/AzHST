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
        string html,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(html);

        var directory = string.IsNullOrWhiteSpace(outputDirectory)
            ? _defaultOutputDirectory
            : outputDirectory.Trim();

        directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(directory);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var fileName = $"azhst-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{suffix}.html";
        var filePath = Path.Combine(directory, fileName);

        await File.WriteAllTextAsync(
            filePath,
            html,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);

        return new VisualizationArtifact(filePath, new Uri(filePath, UriKind.Absolute));
    }
}
