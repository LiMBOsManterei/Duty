namespace Duty.Models;

/// <summary>
/// Профиль подключения к облачной базе (Firebase Realtime Database).
/// </summary>
public sealed class DbConnectionProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Название подключения, которое видит пользователь.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>URL базы вида https://project-default-rtdb.firebaseio.com/.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    public DbConnectionProfile Clone() => new()
    {
        Id = Id,
        Name = Name,
        ConnectionString = ConnectionString,
    };

    /// <summary>
    /// Проверяет и нормализует адрес базы. Возвращает null, если адрес непригоден.
    /// </summary>
    public static string? NormalizeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
        {
            return null;
        }

        // Firebase ожидает адрес без завершающего слэша.
        return uri.ToString().TrimEnd('/');
    }

    /// <summary>Возвращает готовый к работе профиль либо null, если он битый.</summary>
    public DbConnectionProfile? Sanitize()
    {
        var url = NormalizeUrl(ConnectionString);
        if (url is null)
        {
            return null;
        }

        var name = string.IsNullOrWhiteSpace(Name) ? url : Name.Trim();
        return new DbConnectionProfile
        {
            Id = Id == Guid.Empty ? Guid.NewGuid() : Id,
            Name = name,
            ConnectionString = url,
        };
    }
}
