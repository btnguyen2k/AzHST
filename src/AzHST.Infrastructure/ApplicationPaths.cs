namespace AzHST.Infrastructure;

public sealed record ApplicationPaths(
    string DataDirectory,
    string SettingsFile,
    string GeneratedPagesDirectory,
    string CopilotDirectory,
    string SampleQueryDatabaseFile)
{
    public string ProjectDatabaseFile { get; init; } =
        Path.Combine(DataDirectory, "projects.db");

    public static ApplicationPaths CreateDefault()
    {
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localData))
        {
            throw new InvalidOperationException(
                "The operating system did not provide a local application data directory.");
        }

        var dataDirectory = Path.Combine(localData, "AzHST");
        var generatedPagesDirectory = Path.GetFullPath(
            Path.Combine(Environment.CurrentDirectory, "generated"));
        var sampleQueryDatabaseFile = Path.GetFullPath(
            Path.Combine(Environment.CurrentDirectory, "data", "azhst.db"));

        return new ApplicationPaths(
            dataDirectory,
            Path.Combine(dataDirectory, "settings.json"),
            generatedPagesDirectory,
            Path.Combine(dataDirectory, "copilot"),
            sampleQueryDatabaseFile);
    }

    public static ApplicationPaths CreatePortable()
    {
        return CreatePortable(AppContext.BaseDirectory);
    }

    public static ApplicationPaths CreatePortable(string applicationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);

        var applicationRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(applicationDirectory));
        var dataDirectory = Path.Combine(applicationRoot, "data");

        return new ApplicationPaths(
            dataDirectory,
            Path.Combine(dataDirectory, "settings.json"),
            Path.Combine(applicationRoot, "generated"),
            Path.Combine(dataDirectory, "copilot"),
            Path.Combine(dataDirectory, "azhst.db"));
    }
}
