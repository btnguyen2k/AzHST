namespace AzHST.Application.Exceptions;

public class PresentationGenerationException : Exception
{
    public PresentationGenerationException(string message)
        : base(message)
    {
    }

    public PresentationGenerationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
