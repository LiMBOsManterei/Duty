namespace Duty.Services;

/// <summary>Ошибка чтения или записи файла данных.</summary>
public sealed class DataStorageException : Exception
{
    public DataStorageException(string message)
        : base(message)
    {
    }

    public DataStorageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
