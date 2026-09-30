namespace AzHST.Application.Models;

public enum GenerationStage
{
    Connecting,
    Generating,
    Securing,
    Saving,
    Completed,
}

public sealed record GenerationProgress(GenerationStage Stage, string Message);
