using AzHST.Application.Models;

namespace AzHST.Application.Abstractions;

public interface ICopilotProjectConversation
{
    Task<string> CreateAsync(
        string sessionId,
        string query,
        string model,
        string visualizationId,
        HtmlThemeDefinition theme,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<string> RefineAsync(
        string sessionId,
        string originalQuery,
        string followUpRequest,
        string model,
        string visualizationId,
        HtmlThemeDefinition theme,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string sessionId,
        CancellationToken cancellationToken = default);
}
