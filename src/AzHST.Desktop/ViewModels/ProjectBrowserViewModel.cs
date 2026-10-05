using System.Collections.ObjectModel;
using AzHST.Application.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AzHST.Desktop.ViewModels;

public sealed partial class ProjectBrowserViewModel : ObservableObject
{
    private readonly BrowseProjectsUseCase _browseProjectsUseCase;
    private readonly string? _activeProjectId;
    private readonly Action<string> _selectProject;
    private string _appliedSearchText = string.Empty;
    private int _nextOffset;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasMore;

    [ObservableProperty]
    private ProjectBrowserItemViewModel? _selectedProject;

    [ObservableProperty]
    private string _statusMessage = "Loading projects...";

    public ProjectBrowserViewModel(
        BrowseProjectsUseCase browseProjectsUseCase,
        string? activeProjectId,
        Action<string> selectProject)
    {
        _browseProjectsUseCase = browseProjectsUseCase;
        _activeProjectId = activeProjectId;
        _selectProject = selectProject;
    }

    public ObservableCollection<ProjectBrowserItemViewModel> Projects { get; } = [];

    public bool HasProjects => Projects.Count > 0;

    public bool HasNoProjects => !HasProjects;

    public bool IsIdle => !IsLoading;

    public Task InitializeAsync()
    {
        return LoadPageAsync(reset: true);
    }

    private bool CanSearch()
    {
        return !IsLoading;
    }

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private Task SearchAsync()
    {
        return LoadPageAsync(reset: true);
    }

    private bool CanLoadMore()
    {
        return !IsLoading
            && HasMore
            && string.Equals(
                NormalizeSearchText(SearchText),
                _appliedSearchText,
                StringComparison.Ordinal);
    }

    [RelayCommand(CanExecute = nameof(CanLoadMore))]
    private Task LoadMoreAsync()
    {
        return LoadPageAsync(reset: false);
    }

    private bool CanOpenSelected()
    {
        return !IsLoading
            && SelectedProject is not null
            && !SelectedProject.IsActive;
    }

    [RelayCommand(CanExecute = nameof(CanOpenSelected))]
    private void OpenSelected()
    {
        if (SelectedProject is not null)
        {
            _selectProject(SelectedProject.Id);
        }
    }

    private async Task LoadPageAsync(bool reset)
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        StatusMessage = reset
            ? "Searching projects..."
            : "Loading more projects...";

        try
        {
            var offset = reset ? 0 : _nextOffset;
            var requestedSearchText = reset
                ? NormalizeSearchText(SearchText)
                : _appliedSearchText;
            var page = await _browseProjectsUseCase.ExecuteAsync(
                requestedSearchText,
                offset,
                BrowseProjectsUseCase.DefaultPageSize);
            var options = page.Items
                .Select(project => new ProjectBrowserItemViewModel(
                    project,
                    string.Equals(
                        project.Id,
                        _activeProjectId,
                        StringComparison.Ordinal)))
                .ToArray();

            if (reset)
            {
                Projects.Clear();
                _appliedSearchText = requestedSearchText;
            }

            foreach (var option in options)
            {
                Projects.Add(option);
            }

            _nextOffset = page.NextOffset;
            HasMore = page.HasMore;
            if (reset)
            {
                SelectedProject = Projects.FirstOrDefault(
                        project => project.IsActive)
                    ?? Projects.FirstOrDefault();
            }

            OnPropertyChanged(nameof(HasProjects));
            OnPropertyChanged(nameof(HasNoProjects));
            LoadMoreCommand.NotifyCanExecuteChanged();
            StatusMessage = Projects.Count == 0
                ? _appliedSearchText.Length == 0
                    ? "No projects have been created yet."
                    : "No projects match this search."
                : HasMore
                    ? $"Showing the first {Projects.Count} matching projects."
                    : $"Showing {Projects.Count} matching projects.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Could not load projects: {exception.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        LoadMoreCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsLoadingChanged(bool value)
    {
        SearchCommand.NotifyCanExecuteChanged();
        LoadMoreCommand.NotifyCanExecuteChanged();
        OpenSelectedCommand.NotifyCanExecuteChanged();
    }

    partial void OnHasMoreChanged(bool value)
    {
        LoadMoreCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedProjectChanged(
        ProjectBrowserItemViewModel? value)
    {
        OpenSelectedCommand.NotifyCanExecuteChanged();
    }

    private static string NormalizeSearchText(string value)
    {
        return value.Trim();
    }
}
