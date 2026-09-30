using AzHST.Application.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AzHST.Desktop.ViewModels;

public sealed partial class SettingsWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _model;

    [ObservableProperty]
    private string _outputDirectory;

    [ObservableProperty]
    private bool _openResultsInExternalBrowser;

    public SettingsWindowViewModel(AppSettings settings)
    {
        _model = settings.Model;
        _outputDirectory = settings.OutputDirectory;
        _openResultsInExternalBrowser = settings.OpenResultsInExternalBrowser;
    }

    public AppSettings ToSettings()
    {
        return new AppSettings
        {
            Model = Model,
            OutputDirectory = OutputDirectory,
            OpenResultsInExternalBrowser = OpenResultsInExternalBrowser,
        };
    }
}
