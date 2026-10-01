using AzHST.Application.Models;
using CommunityToolkit.Mvvm.Input;

namespace AzHST.Desktop.ViewModels;

public sealed class SampleQueryOptionViewModel
{
    public SampleQueryOptionViewModel(
        SampleQuery sample,
        Action<string> selectQuery)
    {
        CategoryName = sample.CategoryName;
        Query = sample.Query;
        SelectCommand = new RelayCommand(() => selectQuery(Query));
    }

    public string CategoryName { get; }

    public string Query { get; }

    public IRelayCommand SelectCommand { get; }
}
