using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace AzHST.Desktop.Services;

public sealed class ClipboardService : IClipboardService
{
    private readonly Func<Window?> _ownerProvider;

    public ClipboardService(Func<Window?> ownerProvider)
    {
        _ownerProvider = ownerProvider;
    }

    public async Task SetTextAsync(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var owner = _ownerProvider()
            ?? throw new InvalidOperationException("The main window is not available.");
        var clipboard = TopLevel.GetTopLevel(owner)?.Clipboard
            ?? throw new InvalidOperationException("The system clipboard is not available.");

        await clipboard.SetTextAsync(text);
    }
}
