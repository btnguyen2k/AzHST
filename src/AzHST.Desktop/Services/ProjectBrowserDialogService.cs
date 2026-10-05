using Avalonia.Controls;
using AzHST.Application.Services;
using AzHST.Desktop.ViewModels;
using AzHST.Desktop.Views;

namespace AzHST.Desktop.Services;

public sealed class ProjectBrowserDialogService
    : IProjectBrowserDialogService
{
    private readonly Func<Window?> _ownerProvider;
    private readonly BrowseProjectsUseCase _browseProjectsUseCase;

    public ProjectBrowserDialogService(
        Func<Window?> ownerProvider,
        BrowseProjectsUseCase browseProjectsUseCase)
    {
        _ownerProvider = ownerProvider;
        _browseProjectsUseCase = browseProjectsUseCase;
    }

    public Task<string?> ShowAsync(string? activeProjectId)
    {
        var owner = _ownerProvider()
            ?? throw new InvalidOperationException(
                "The main window is not available.");
        ProjectBrowserWindow? window = null;
        var viewModel = new ProjectBrowserViewModel(
            _browseProjectsUseCase,
            activeProjectId,
            projectId => window?.Close(projectId));
        window = new ProjectBrowserWindow
        {
            DataContext = viewModel,
        };
        window.Opened += async (_, _) => await viewModel.InitializeAsync();

        return window.ShowDialog<string?>(owner);
    }
}
