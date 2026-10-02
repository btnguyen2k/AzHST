namespace AzHST.Application.Models;

public sealed record AuthenticationStatus(
    bool IsAuthenticated,
    string Message,
    string? UserName = null);
