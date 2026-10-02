namespace AzHST.Application.Exceptions;

public sealed class SampleQueryGenerationException : Exception
{
    public SampleQueryGenerationException(string message)
        : base(message)
    {
    }

    public SampleQueryGenerationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
