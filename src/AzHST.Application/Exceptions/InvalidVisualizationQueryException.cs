namespace AzHST.Application.Exceptions;

public sealed class InvalidVisualizationQueryException : VisualizationGenerationException
{
    public InvalidVisualizationQueryException(string message)
        : base(message)
    {
    }
}
