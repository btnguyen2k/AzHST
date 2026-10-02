namespace AzHST.Application.Models;

public sealed record VisualizationArtifact(
    string Id,
    string DirectoryPath,
    string FilePath,
    Uri FileUri)
{
    public string Html { get; init; } = string.Empty;
}
