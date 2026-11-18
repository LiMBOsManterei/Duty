namespace Duty.Services;

/// <summary>Ошибка при работе с облачной базой.</summary>
public sealed class CloudDataException : Exception
{
    public CloudDataException(string message)
        : base(message)
    {
    }

    public CloudDataException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
