using Duty.Models;

namespace Duty.Services;

/// <summary>Хранилище данных приложения.</summary>
public interface IDutyDataService
{
    /// <summary>Путь к JSON-файлу (полезно для диагностики).</summary>
    string StoragePath { get; }

    /// <summary>Возвращает null, если файла ещё нет.</summary>
    Task<DutyData?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(DutyData data, CancellationToken cancellationToken = default);
}
