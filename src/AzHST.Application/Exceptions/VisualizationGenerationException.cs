namespace AzHST.Application.Exceptions;

public sealed class VisualizationGenerationException : Exception
{
    public VisualizationGenerationException(string message)
        : base(message)
    {
    }

    public VisualizationGenerationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
