namespace AzHST.Application.Models;

public static class CopilotModelSelection
{
    public const string Automatic = "auto";

    public static string Normalize(string? model)
    {
        var normalized = model?.Trim();
        if (string.IsNullOrEmpty(normalized)
            || string.Equals(
                normalized,
                Automatic,
                StringComparison.OrdinalIgnoreCase))
        {
            return Automatic;
        }

        return normalized;
    }

    public static bool IsAutomatic(string? model)
    {
        return string.Equals(
            Normalize(model),
            Automatic,
            StringComparison.Ordinal);
    }
}
