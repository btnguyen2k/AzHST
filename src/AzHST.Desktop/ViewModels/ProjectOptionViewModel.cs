using AzHST.Application.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AzHST.Desktop.ViewModels;

public sealed partial class ProjectOptionViewModel : ObservableObject
{
    public ProjectOptionViewModel(
        Project project,
        bool isActive,
        Func<string, Task> openProject)
    {
        Id = project.Id;
        Title = project.Title;
        UpdatedText = $"Updated {project.UpdatedUtc.ToLocalTime():g}";
        IsActive = isActive;
        OpenCommand = new AsyncRelayCommand(() => openProject(Id));
    }

    public string Id { get; }

    public string Title { get; }

    public string UpdatedText { get; }

    [ObservableProperty]
    private bool _isActive;

    public IAsyncRelayCommand OpenCommand { get; }
}
