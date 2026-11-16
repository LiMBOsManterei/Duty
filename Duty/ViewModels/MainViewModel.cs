using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Duty.Common;
using Duty.Models;
using Duty.Services;
using Firebase.Database;
using Firebase.Database.Query;

namespace Duty.ViewModels;

/// <summary>
/// ViewModel единственной страницы: состояние баланса, список трат и сценарии.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IDutyDataService _dataService;
    private readonly IDialogService _dialogs;
    private DutyData _data = new();
    private bool _isLoading;

    public MainViewModel(IDutyDataService dataService, IDialogService dialogs)
    {
        _dataService = dataService;
        _dialogs = dialogs;

        ExpenseEditor = new ExpenseEditorViewModel(SaveExpenseAsync, DeleteExpenseAsync, dialogs);
        BalanceEditor = new BalanceEditorViewModel(SaveBalanceAsync);

        IsBusy = true;
    }

    public ObservableCollection<ExpenseItemViewModel> Expenses { get; } = new();

    public ExpenseEditorViewModel ExpenseEditor { get; }

    public BalanceEditorViewModel BalanceEditor { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedBalance))]
    public partial decimal TotalBalance { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsInitialized { get; set; }

    public string FormattedBalance => MoneyFormatter.Format(TotalBalance);

    public bool IsNotBusy => !IsBusy;

    public bool HasNoExpenses => Expenses.Count == 0;

    public string StoragePath => _dataService.StoragePath;

    // Индикатор активной отправки (крутит спиннер и блокирует повторное нажатие)
    [ObservableProperty]
    private bool _isSyncing;

    // Текст статуса (например: "Сохранено в облако" или "Ошибка отправки")
    [ObservableProperty]
    private string _syncStatusText = string.Empty;

    // Видимость плашки со статусом
    [ObservableProperty]
    private bool _isSyncStatusVisible;

    // Цвет плашки (зеленый при успехе, красный при ошибке)
    [ObservableProperty]
    private Color _syncStatusColor = Color.FromArgb("#34C759");


    /// <summary>Вызывается страницей один раз при появлении.</summary>
    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (IsInitialized)
        {
            return;
        }

        IsInitialized = true;
        await LoadAsync();
    }

    /// <summary>Перечитывает JSON-файл с диска.</summary>
    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private void AddExpense() => ExpenseEditor.OpenForCreate();

    private CancellationTokenSource? _statusCts;

    [RelayCommand]
    private async Task SendDbToFirebase()
    {
        var confirmed = await _dialogs.ConfirmAsync(
                "Подтверждение",
                "\n ДА Отправить. (защита от случайных нажатий)",
                "ДА", "Отмена");

        if (!confirmed)
        {
            return;
        }


        if (IsSyncing) return;
        try
        {
            IsSyncing = true;
            IsSyncStatusVisible = false;

            var firebaseClient = new FirebaseClient("https://dutydb-47b3c-default-rtdb.firebaseio.com/");
            await firebaseClient.Child("duty_data").PutAsync(_data);

            SyncStatusColor = Color.FromArgb("#34C759"); // Зеленый (iOS/Material Green)
            SyncStatusText = $"Отправлено в {DateTime.Now:HH:mm}";
            IsSyncStatusVisible = true;
            _ = HideStatusAfterDelayAsync(_statusCts.Token);
        }
        catch (Exception ex)
        {
            // Показываем метку об ошибке, если нет интернета или неверный URL
            SyncStatusColor = Color.FromArgb("#FF3B30"); // Ваш цвет Danger
            SyncStatusText = $"Ошибка отправки, {ex.Message}" ;
            IsSyncStatusVisible = true;

            //await _dialogs.ShowAsync(
            //    "Не удалось отправить данные в Firebase",
            //    ex.Message);
        }
        finally
        {
            IsSyncing = false;
        }


    }

    /// <summary>
    /// Показывает всплывающее уведомление внизу экрана.
    /// </summary>
    /// <param name="text">Текст сообщения.</param>
    /// <param name="isSuccess">true — зеленый фон (успех), false — красный фон (ошибка).</param>
    private void ShowStatusMessage(string text, bool isSuccess)
    {
        // Отменяем предыдущий таймер скрытия, если плашка уже висит на экране
        _statusCts?.Cancel();
        _statusCts?.Dispose();
        _statusCts = new CancellationTokenSource();

        SyncStatusText = text;
        SyncStatusColor = isSuccess
            ? Color.FromArgb("#34C759") // Зеленый (успех)
            : Color.FromArgb("#FF3B30"); // Красный (ошибка)

        IsSyncStatusVisible = true;

        // Запускаем автоматическое скрытие через 3 секунды
        _ = HideStatusAfterDelayAsync(_statusCts.Token);
    }

    [RelayCommand]
    public async Task AllDelete()
    {
        try
        {
           
            if (_data != null)
            {
                int count = _data.Expenses.Count;
                var d = _data.Clone();
                if (_data.Expenses != null && _data.Expenses.Count > 0)
                {
                    foreach (Expense item in d.Expenses)
                    {
                        await DeleteExpenseAsync(item.Id);
                    }

                    ShowStatusMessage($"Удалено {count} записей", true);

                    return;
                }
            }

            ShowStatusMessage("Записи не найдены", false);

        }
        catch (Exception ex)
        {
            ShowStatusMessage($"Ошибка {ex.Message}", false);
        }

       
        
    }

    private async Task HideStatusAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(3000, token);
            IsSyncStatusVisible = false;
        }
        catch (TaskCanceledException)
        {
            // Игнорируем отмену, если за это время появилось новое сообщение
        }
    }
    [RelayCommand]
    private async Task LoadFireDb()
    {
        try
        {
            FirebaseDataService service = new FirebaseDataService(new FirebaseClient("https://dutydb-47b3c-default-rtdb.firebaseio.com/"));
            DutyData duty = await service.LoadDataAsync();
            if (duty != null)
            {
                _data = duty;
                ApplyData();
            }
        }
        catch (Exception ex)
        {
            await _dialogs.ShowAsync(
                "Не удалось загрузить данные из Firebase",
                ex.Message);
        }
    }

    [RelayCommand]
    private void EditBalance() => BalanceEditor.Open(TotalBalance);

    private void EditExpense(ExpenseItemViewModel item) => ExpenseEditor.OpenForEdit(item.Model);

    public async void DeleteFast(ExpenseItemViewModel item) => await DeleteExpenseAsync(item.Model.Id);
    private async Task LoadAsync()
    {
        if (_isLoading)
        {
            return;
        }

        _isLoading = true;
        IsBusy = true;
        try
        {
            var loaded = await _dataService.LoadAsync();
            if (loaded is null)
            {
                _data = CreateSeedData();
                await TrySeedAsync(_data);
                await SendDbToFirebase();
            }
            else
            {

                await LoadFireDb();

                return;
            }

            ApplyData();
        }
        catch (Exception ex)
        {
            _data = CreateSeedData();
            ApplyData();
            await _dialogs.ShowAsync(
                "Не удалось загрузить данные",
                $"{ex.Message}\n\nПоказаны демонстрационные данные. Проверьте файл:\n{_dataService.StoragePath}");
        }
        finally
        {
            IsBusy = false;
            _isLoading = false;
        }
    }

    private async Task TrySeedAsync(DutyData data)
    {
        try
        {
            await _dataService.SaveAsync(data);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowAsync(
                "Не удалось создать файл данных",
                $"{ex.Message}\n\nПриложение продолжит работу, но изменения могут не сохраниться.");
        }
    }

    private void ApplyData()
    {
        TotalBalance = _data.TotalBalance;

        Expenses.Clear();
        foreach (var expense in _data.Expenses
                     .OrderByDescending(item => item.CreatedAt)
                     .ThenByDescending(item => item.Id))
        {
            Expenses.Add(new ExpenseItemViewModel(expense, EditExpense, DeleteFast));
        }

        OnPropertyChanged(nameof(HasNoExpenses));
    }

    private async Task<bool> SaveExpenseAsync(ExpenseDraft draft)
    {
        var candidate = _data.Clone();

        if (draft.Id is null)
        {
            candidate.Expenses.Add(new Expense
            {
                Id = Guid.NewGuid(),
                Name = draft.Name,
                Amount = draft.Amount,
                CategoryKey = draft.CategoryKey,
                CreatedAt = DateTimeOffset.Now,
            });
            candidate.TotalBalance -= draft.Amount;
        }
        else
        {
            var existing = candidate.Expenses.FirstOrDefault(item => item.Id == draft.Id.Value);
            if (existing is null)
            {
                await _dialogs.ShowAsync(
                    "Трата не найдена",
                    "Возможно, запись уже была удалена. Обновите данные кнопкой в шапке.");
                return false;
            }

            candidate.TotalBalance += existing.Amount - draft.Amount;
            existing.Name = draft.Name;
            existing.Amount = draft.Amount;
            existing.CategoryKey = draft.CategoryKey;
        }

        candidate.UpdatedAt = DateTimeOffset.Now;
        if (!await TryPersistAsync(candidate))
        {
            return false;
        }

        _data = candidate;
        ApplyData();
        return true;
    }

    private async Task<bool> DeleteExpenseAsync(Guid id)
    {
        var candidate = _data.Clone();
        var existing = candidate.Expenses.FirstOrDefault(item => item.Id == id);
        if (existing is null)
        {
            await _dialogs.ShowAsync(
                "Трата не найдена",
                "Возможно, запись уже была удалена. Обновите данные кнопкой в шапке.");
            return false;
        }

        candidate.Expenses.Remove(existing);
        candidate.TotalBalance += existing.Amount;
        candidate.UpdatedAt = DateTimeOffset.Now;

        if (!await TryPersistAsync(candidate))
        {
            return false;
        }

        _data = candidate;
        ApplyData();
        return true;
    }

    private async Task<bool> SaveBalanceAsync(decimal amount)
    {
        var candidate = _data.Clone();
        candidate.TotalBalance = amount;
        candidate.UpdatedAt = DateTimeOffset.Now;

        if (!await TryPersistAsync(candidate))
        {
            return false;
        }

        _data = candidate;
        ApplyData();
        return true;
    }

    private async Task<bool> TryPersistAsync(DutyData candidate)
    {
        try
        {
            await _dataService.SaveAsync(candidate);
            return true;
        }
        catch (Exception ex)
        {
            await _dialogs.ShowAsync(
                "Не удалось сохранить",
                $"{ex.Message}\n\nИзменения не применены. Проверьте доступ к файлу:\n{_dataService.StoragePath}");
            return false;
        }
    }

    private static DutyData CreateSeedData()
    {
        var now = DateTimeOffset.Now;

        return new DutyData
        {
            TotalBalance = 0,
            UpdatedAt = now,
            Expenses = new List<Expense>()

        };
    }
}
