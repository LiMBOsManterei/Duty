using System.Text.Encodings.Web;
using System.Text.Json;
using Duty.Models;

namespace Duty.Services;

/// <summary>
/// Хранит профили подключений в JSON рядом с данными приложения.
/// При первом запуске создаёт одно подключение с адресом проекта по умолчанию.
/// </summary>
public sealed class ConnectionProfileService : IConnectionProfileService
{
    /// <summary>Адрес базы, который использовался в приложении до появления профилей.</summary>
    public const string DefaultConnectionString = "https://dutydb-47b3c-default-rtdb.firebaseio.com/";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _filePath;
    private List<DbConnectionProfile>? _cache;
    private Guid? _activeId;

    public ConnectionProfileService(string? filePath = null)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath) ? LocalStoragePaths.ConnectionsFile : filePath;
    }

    public async Task<IReadOnlyList<DbConnectionProfile>> GetProfilesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        return _cache!.Select(profile => profile.Clone()).ToList();
    }

    public async Task<DbConnectionProfile?> GetActiveProfileAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        var active = _cache!.FirstOrDefault(profile => profile.Id == _activeId)
                     ?? _cache!.FirstOrDefault();

        return active?.Clone();
    }

    public async Task SaveAsync(
        IEnumerable<DbConnectionProfile> profiles,
        Guid? activeProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _cache = Sanitize(profiles);
            _activeId = activeProfileId is Guid id && _cache.Any(profile => profile.Id == id)
                ? id
                : _cache.FirstOrDefault()?.Id;

            await WriteAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetActiveAsync(Guid? profileId, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _activeId = profileId is Guid id && _cache!.Any(profile => profile.Id == id)
                ? id
                : _cache!.FirstOrDefault()?.Id;

            await WriteAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_cache is not null)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cache is not null)
            {
                return;
            }

            StoredProfiles? stored = null;
            if (File.Exists(_filePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
                    stored = JsonSerializer.Deserialize<StoredProfiles>(json, SerializerOptions);
                }
                catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
                {
                    stored = null;
                }
            }

            var profiles = Sanitize(stored?.Profiles ?? new List<DbConnectionProfile>());
            if (profiles.Count == 0)
            {
                profiles.Add(new DbConnectionProfile
                {
                    Name = "Основное",
                    ConnectionString = DefaultConnectionString,
                });
            }

            _cache = profiles;
            _activeId = stored?.ActiveProfileId is Guid id && profiles.Any(profile => profile.Id == id)
                ? id
                : profiles[0].Id;

            await WriteAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static List<DbConnectionProfile> Sanitize(IEnumerable<DbConnectionProfile> profiles)
    {
        var result = new List<DbConnectionProfile>();
        var ids = new HashSet<Guid>();

        foreach (var profile in profiles)
        {
            if (profile is null)
            {
                continue;
            }

            var clean = profile.Sanitize();
            if (clean is null || !ids.Add(clean.Id))
            {
                continue;
            }

            result.Add(clean);
        }

        return result;
    }

    private async Task WriteAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var payload = new StoredProfiles
        {
            ActiveProfileId = _activeId,
            Profiles = _cache ?? new List<DbConnectionProfile>(),
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, SerializerOptions);
        var tempPath = _filePath + ".tmp";
        await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken).ConfigureAwait(false);
        File.Move(tempPath, _filePath, overwrite: true);
    }

    private sealed class StoredProfiles
    {
        public Guid? ActiveProfileId { get; set; }

        public List<DbConnectionProfile> Profiles { get; set; } = new();
    }
}
