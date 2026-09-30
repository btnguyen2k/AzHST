namespace AzHST.Application.Models;

public sealed record VisualizationArtifact(
    string Id,
    string DirectoryPath,
    string FilePath,
    Uri FileUri);
