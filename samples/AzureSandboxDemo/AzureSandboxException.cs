namespace AzureSandboxDemo;

internal sealed class AzureSandboxException : Exception
{
    public AzureSandboxException(string message)
        : base(message)
    {
    }

    public AzureSandboxException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
