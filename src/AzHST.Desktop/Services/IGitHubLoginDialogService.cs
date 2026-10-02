namespace AzHST.Desktop.Services;

public interface IGitHubLoginDialogService
{
    Task<bool> ShowAsync();
}
