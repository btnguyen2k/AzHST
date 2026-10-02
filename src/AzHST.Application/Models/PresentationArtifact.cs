namespace AzHST.Application.Models;

public sealed record PresentationArtifact(
    string VisualizationId,
    string FilePath,
    Uri FileUri,
    int SlideCount);
