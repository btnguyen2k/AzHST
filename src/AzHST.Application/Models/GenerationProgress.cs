namespace AzHST.Application.Models;

public enum GenerationStage
{
    Connecting,
    Assessing,
    Generating,
    Securing,
    Saving,
    Completed,
}

public sealed record GenerationProgress(GenerationStage Stage, string Message);
