using Duty.Models;
using Firebase.Database;
using Firebase.Database.Query;

namespace Duty.Services;

/// <summary>
/// Клиент Firebase Realtime Database. Соединение создаётся под конкретный профиль,
/// каждый запрос ограничен таймаутом, чтобы UI не ждал сеть бесконечно.
/// </summary>
public sealed class FirebaseDataService : ICloudDataService
{
    private const string RootNode = "duty_data";

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(8);

    public async Task<DutyData?> LoadAsync(DbConnectionProfile profile, CancellationToken cancellationToken = default)
    {
        var url = DbConnectionProfile.NormalizeUrl(profile.ConnectionString)
                  ?? throw new CloudDataException("Некорректный адрес базы данных. Проверьте подключение.");

        using var client = new FirebaseClient(url);
        try
        {
            var data = await client
                .Child(RootNode)
                .OnceSingleAsync<DutyData>(DefaultTimeout)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            return data;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException ex)
        {
            throw new CloudDataException("Облачная база не ответила вовремя.", ex);
        }
        catch (Exception ex)
        {
            throw new CloudDataException("Не удалось прочитать данные из облака.", ex);
        }
    }

    public async Task SaveAsync(DbConnectionProfile profile, DutyData data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        var url = DbConnectionProfile.NormalizeUrl(profile.ConnectionString)
                  ?? throw new CloudDataException("Некорректный адрес базы данных. Проверьте подключение.");

        using var client = new FirebaseClient(url);
        try
        {
            // У PutAsync в этой версии клиента нет параметра таймаута,
            // поэтому ограничение задаётся отменой ожидания.
            await client
                .Child(RootNode)
                .PutAsync(data)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException ex)
        {
            throw new CloudDataException("Облачная база не ответила вовремя.", ex);
        }
        catch (Exception ex)
        {
            throw new CloudDataException("Не удалось отправить данные в облако.", ex);
        }
    }
}
