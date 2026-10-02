namespace AzHST.Application.Models;

public sealed record CopilotModelOption(string Id, string DisplayName)
{
    public static CopilotModelOption Automatic { get; } = new(
        CopilotModelSelection.Automatic,
        "Automatic (recommended)");
}
