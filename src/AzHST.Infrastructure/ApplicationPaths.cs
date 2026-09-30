namespace AzHST.Infrastructure;

public sealed record ApplicationPaths(
    string DataDirectory,
    string SettingsFile,
    string GeneratedPagesDirectory,
    string CopilotDirectory)
{
    public static ApplicationPaths CreateDefault()
    {
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localData))
        {
            throw new InvalidOperationException(
                "The operating system did not provide a local application data directory.");
        }

        var dataDirectory = Path.Combine(localData, "AzHST");
        return new ApplicationPaths(
            dataDirectory,
            Path.Combine(dataDirectory, "settings.json"),
            Path.Combine(dataDirectory, "generated"),
            Path.Combine(dataDirectory, "copilot"));
    }
}
