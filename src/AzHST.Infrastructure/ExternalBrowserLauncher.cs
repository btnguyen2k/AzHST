using System.Diagnostics;
using AzHST.Application.Abstractions;

namespace AzHST.Infrastructure;

public sealed class ExternalBrowserLauncher : IExternalBrowser, IExternalFileLauncher
{
    public void Open(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = uri.AbsoluteUri,
            UseShellExecute = true,
        });

        if (process is null)
        {
            throw new InvalidOperationException(
                "The operating system could not open the default application.");
        }

        process.Dispose();
    }

    public void OpenContainingFolder(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "The file no longer exists.",
                fullPath);
        }

        var directoryPath = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException(
                "The file does not have a containing folder.");
        var folderPath = Path.EndsInDirectorySeparator(directoryPath)
            ? directoryPath
            : directoryPath + Path.DirectorySeparatorChar;

        Open(new Uri(folderPath));
    }
}
