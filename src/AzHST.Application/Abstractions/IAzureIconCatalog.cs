using AzHST.Application.Models;

namespace AzHST.Application.Abstractions;

public interface IAzureIconCatalog
{
    IReadOnlyList<AzureIconDescriptor> FindRelevant(
        string query,
        int maximumResults);

    bool TryGetDataUri(string key, out string dataUri);
}
