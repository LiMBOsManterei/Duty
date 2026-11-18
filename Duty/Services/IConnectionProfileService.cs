using Duty.Models;

namespace Duty.Services;

/// <summary>Локальное хранилище профилей подключения к облачной базе.</summary>
public interface IConnectionProfileService
{
    /// <summary>Список сохранённых подключений.</summary>
    Task<IReadOnlyList<DbConnectionProfile>> GetProfilesAsync(CancellationToken cancellationToken = default);

    /// <summary>Текущее активное подключение (null — облако не выбрано).</summary>
    Task<DbConnectionProfile?> GetActiveProfileAsync(CancellationToken cancellationToken = default);

    /// <summary>Сохраняет список подключений и активный профиль.</summary>
    Task SaveAsync(
        IEnumerable<DbConnectionProfile> profiles,
        Guid? activeProfileId,
        CancellationToken cancellationToken = default);

    /// <summary>Переключает активное подключение.</summary>
    Task SetActiveAsync(Guid? profileId, CancellationToken cancellationToken = default);
}
