namespace AzHST.Application.Exceptions;

public sealed class OutputThemeConfigurationException : Exception
{
    public OutputThemeConfigurationException(string message)
        : base(message)
    {
    }

    public OutputThemeConfigurationException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}
