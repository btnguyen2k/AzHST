namespace AzHST.Desktop.Composition;

internal sealed record DebugTestOptions(
    bool SkipGitHubSignInAtStartup,
    bool SimulateMissingGitHubCli)
{
    public static DebugTestOptions FromEnvironment()
    {
#if DEBUG
        return new DebugTestOptions(
            IsDefined("TEST_NO_GH_SIGNIN"),
            IsDefined("TEST_NO_GH_BIN"));
#else
        return new DebugTestOptions(false, false);
#endif
    }

#if DEBUG
    private static bool IsDefined(string variableName)
    {
        return Environment.GetEnvironmentVariable(variableName) is not null;
    }
#endif
}
