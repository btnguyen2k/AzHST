namespace AzHST.Desktop.Services;

public interface IProjectBrowserDialogService
{
    Task<string?> ShowAsync(string? activeProjectId);
}
