using System.Text.Encodings.Web;
using System.Text.Json;
using Duty.Models;

namespace Duty.Services;

/// <summary>
/// JSON-хранилище локального снимка. Пишет атомарно через временный файл,
/// держит резервную копию и карантинит повреждённый JSON.
/// </summary>
public sealed class JsonDutyDataService : IDutyDataService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        // Кириллица должна оставаться читаемой в файле, а не \uXXXX.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _filePath;

    public JsonDutyDataService(string? filePath = null)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath) ? LocalStoragePaths.DataFile : filePath;
    }

    public string StoragePath => _filePath;

    private string BackupPath => _filePath + ".bak";

    /// <summary>
    /// Читает локальный снимок данных. Возвращает null, если файла ещё нет.
    /// </summary>
    public async Task<DutyData?> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            try
            {
                var data = await ReadAsync(_filePath, cancellationToken).ConfigureAwait(false)
                           ?? throw new DataStorageException("Файл данных пуст.");

                data.EnsureIntegrity();
                return data;
            }
            catch (JsonException ex)
            {
                var quarantinePath = Quarantine(_filePath);
                var backup = await TryReadBackupAsync(cancellationToken).ConfigureAwait(false);
                if (backup is not null)
                {
                    backup.EnsureIntegrity();
                    return backup;
                }

                throw new DataStorageException(
                    $"Файл данных повреждён. Копия сохранена: {quarantinePath}",
                    ex);
            }
            catch (IOException ex)
            {
                throw new DataStorageException("Не удалось прочитать файл данных.", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new DataStorageException("Нет доступа к файлу данных.", ex);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(DutyData data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = _filePath + ".tmp";
            var payload = JsonSerializer.SerializeToUtf8Bytes(data, SerializerOptions);
            await File.WriteAllBytesAsync(tempPath, payload, cancellationToken).ConfigureAwait(false);

            if (File.Exists(_filePath))
            {
                try
                {
                    File.Copy(_filePath, BackupPath, overwrite: true);
                }
                catch (IOException)
                {
                    // Резервная копия не критична — основная запись продолжится.
                }
                catch (UnauthorizedAccessException)
                {
                    // См. выше.
                }
            }

            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new DataStorageException("Не удалось сохранить данные.", ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<DutyData?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        if (bytes.Length == 0)
        {
            return null;
        }

        return JsonSerializer.Deserialize<DutyData>(bytes, SerializerOptions);
    }

    private async Task<DutyData?> TryReadBackupAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(BackupPath))
        {
            return null;
        }

        try
        {
            return await ReadAsync(BackupPath, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string Quarantine(string path)
    {
        var target = $"{path}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
        try
        {
            File.Move(path, target, overwrite: true);
        }
        catch (IOException)
        {
            // Если файл занят, оставляем его на месте.
        }
        catch (UnauthorizedAccessException)
        {
            // См. выше.
        }

        return target;
    }
}
