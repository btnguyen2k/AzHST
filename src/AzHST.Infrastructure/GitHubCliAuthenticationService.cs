using System.ComponentModel;
using System.Diagnostics;
using AzHST.Application.Abstractions;
using AzHST.Application.Models;

namespace AzHST.Infrastructure;

public sealed class GitHubCliAuthenticationService : IGitHubAuthenticationService
{
    private const string MissingGitHubCliMessage =
        "GitHub CLI was not found. Install gh, then sign in.";

    private readonly bool _simulateMissingExecutable;

    public GitHubCliAuthenticationService(bool simulateMissingExecutable = false)
    {
        _simulateMissingExecutable = simulateMissingExecutable;
    }

    public async Task<AuthenticationStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        if (_simulateMissingExecutable)
        {
            return new AuthenticationStatus(false, MissingGitHubCliMessage);
        }

        ProcessResult result;
        try
        {
            result = await RunAsync(
                ["api", "user", "--jq", ".login"],
                cancellationToken);
        }
        catch (Win32Exception)
        {
            return new AuthenticationStatus(
                false,
                MissingGitHubCliMessage);
        }

        var userName = result.StandardOutput.Trim();
        if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(userName))
        {
            return new AuthenticationStatus(
                true,
                $"Signed in as {userName}",
                userName);
        }

        return new AuthenticationStatus(
            false,
            "Not signed in. Open the GitHub sign-in instructions to continue.");
    }

    private static async Task<ProcessResult> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "gh",
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            },
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        if (!process.Start())
        {
            throw new InvalidOperationException("GitHub CLI could not be started.");
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        return new ProcessResult(
            process.ExitCode,
            await standardOutput,
            await standardError);
    }

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
