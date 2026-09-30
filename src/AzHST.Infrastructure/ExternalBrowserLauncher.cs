using System.Diagnostics;
using AzHST.Application.Abstractions;

namespace AzHST.Infrastructure;

public sealed class ExternalBrowserLauncher : IExternalBrowser
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
            throw new InvalidOperationException("The operating system could not open the default browser.");
        }

        process.Dispose();
    }
}
