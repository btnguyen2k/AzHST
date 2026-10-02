using Avalonia.Controls;
using AzHST.Desktop.Views;

namespace AzHST.Desktop.Services;

public sealed class GitHubLoginDialogService : IGitHubLoginDialogService
{
    private readonly Func<Window?> _ownerProvider;

    public GitHubLoginDialogService(Func<Window?> ownerProvider)
    {
        _ownerProvider = ownerProvider;
    }

    public Task<bool> ShowAsync()
    {
        var owner = _ownerProvider()
            ?? throw new InvalidOperationException("The main window is not available.");

        return new GitHubLoginWindow().ShowDialog<bool>(owner);
    }
}
