using AzHST.Application.Models;

namespace AzHST.Application.Abstractions;

public interface IGitHubAuthenticationService
{
    Task<AuthenticationStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}
