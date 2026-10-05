using System.Diagnostics.CodeAnalysis;
using AzHST.Application.Models;

namespace AzHST.Application.Abstractions;

public interface IAzureIconCatalog
{
    IReadOnlyList<AzureIconDescriptor> FindRelevant(
        string query,
        int maximumResults);

    bool TryGet(
        string key,
        [NotNullWhen(true)] out AzureIconDescriptor? descriptor,
        out string dataUri);

    bool TryGetDataUri(string key, out string dataUri)
    {
        return TryGet(key, out _, out dataUri);
    }
}
