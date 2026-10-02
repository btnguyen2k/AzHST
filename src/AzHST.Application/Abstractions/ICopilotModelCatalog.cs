using AzHST.Application.Models;

namespace AzHST.Application.Abstractions;

public interface ICopilotModelCatalog
{
    Task<IReadOnlyList<CopilotModelOption>> ListModelsAsync(
        CancellationToken cancellationToken = default);
}
