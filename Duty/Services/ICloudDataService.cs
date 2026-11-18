using Duty.Models;

namespace Duty.Services;

/// <summary>Чтение и запись снимка данных в облачную базу.</summary>
public interface ICloudDataService
{
    /// <summary>
    /// Читает данные из базы профиля. Возвращает null, если в базе пусто.
    /// По истечении таймаута бросает <see cref="CloudDataException"/>.
    /// </summary>
    Task<DutyData?> LoadAsync(DbConnectionProfile profile, CancellationToken cancellationToken = default);

    /// <summary>Полностью перезаписывает данные в базе профиля.</summary>
    Task SaveAsync(DbConnectionProfile profile, DutyData data, CancellationToken cancellationToken = default);
}
