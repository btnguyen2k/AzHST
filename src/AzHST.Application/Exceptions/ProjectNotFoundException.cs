namespace AzHST.Application.Exceptions;

public sealed class ProjectNotFoundException : Exception
{
    public ProjectNotFoundException(string projectId)
        : base($"Project '{projectId}' was not found.")
    {
    }
}
