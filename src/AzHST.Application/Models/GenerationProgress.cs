namespace AzHST.Application.Models;

public enum GenerationStage
{
    Connecting,
    Assessing,
    Generating,
    Securing,
    Saving,
    PlanningPresentation,
    BuildingPresentation,
    Completed,
}

public sealed record GenerationProgress(GenerationStage Stage, string Message);
