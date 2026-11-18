using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Duty.Common;
using Duty.Models;
using Duty.Services;
using Microsoft.Maui.Graphics;

namespace Duty.ViewModels;

/// <summary>Вид статусного сообщения.</summary>
public enum SyncStatusKind
{
    Success,
    Warning,
    Error,
}

/// <summary>
/// ViewModel единственной страницы: баланс, список трат, облачная синхронизация,
/// подключения и категории.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    /// <summary>Сколько ждём облако, прежде чем уйти в офлайн-режим.</summary>
    private static readonly TimeSpan CloudWait = TimeSpan.FromSeconds(8);

    private readonly IDutyDataService _dataService;
    private readonly IConnectionProfileService _profiles;
    private readonly ICloudDataService _cloud;
    private readonly IDialogService _dialogs;

    private DutyData _data = new();
    private bool _isLoading;
    private CancellationTokenSource? _statusCts;

    public MainViewModel(
        IDutyDataService dataService,
        IConnectionProfileService profiles,
        ICloudDataService cloud,
        IDialogService dialogs)
    {
        _dataService = dataService;
        _profiles = profiles;
        _cloud = cloud;
        _dialogs = dialogs;

        ExpenseEditor = new ExpenseEditorViewModel(SaveExpenseAsync, DeleteExpenseAsync, dialogs);
        BalanceEditor = new BalanceEditorViewModel(SaveBalanceAsync);

        Connections = new ConnectionsViewModel(profiles, dialogs, OnConnectionActivatedAsync);
        Categories = new CategoriesViewModel(dialogs, GetCategories, SaveCategoryAsync, DeleteCategoryAsync);

        IsBusy = true;
        ActiveConnectionText = "Подключение не выбрано";
    }

    public ObservableCollection<ExpenseItemViewModel> Expenses { get; } = new();

    public ExpenseEditorViewModel ExpenseEditor { get; }

    public BalanceEditorViewModel BalanceEditor { get; }

    public ConnectionsViewModel Connections { get; }

    public CategoriesViewModel Categories { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedBalance))]
    public partial decimal TotalBalance { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsInitialized { get; set; }

    /// <summary>Идёт отправка данных в облако.</summary>
    [ObservableProperty]
    public partial bool IsSyncing { get; set; }

    /// <summary>Показаны локальные данные, потому что облако недоступно.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowLocalModeHint))]
    public partial bool IsLocalMode { get; set; }

    /// <summary>Пояснение к офлайн-режиму: «Локальные данные на 19.11 00:55».</summary>
    [ObservableProperty]
    public partial string LocalModeText { get; set; } = string.Empty;

    /// <summary>Название активного подключения.</summary>
    [ObservableProperty]
    public partial string ActiveConnectionText { get; set; }

    [ObservableProperty]
    public partial string SyncStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSyncStatusVisible { get; set; }

    [ObservableProperty]
    public partial Color SyncStatusColor { get; set; } = Color.FromArgb("#34C759");

    [ObservableProperty]
    public partial string SyncStatusIcon { get; set; } = "\uf00c";

    public string FormattedBalance => MoneyFormatter.Format(TotalBalance);

    public bool IsNotBusy => !IsBusy;

    public bool HasNoExpenses => Expenses.Count == 0;

    public bool ShowLocalModeHint => IsLocalMode && !string.IsNullOrWhiteSpace(LocalModeText);

    public string StoragePath => _dataService.StoragePath;

    /// <summary>Вызывается страницей один раз при появлении.</summary>
    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (IsInitialized)
        {
            return;
        }

        IsInitialized = true;
        await ReloadAsync();
    }

    /// <summary>Обновление экрана: сначала облако, затем локальный снимок.</summary>
    [RelayCommand]
    private Task RefreshAsync() => ReloadAsync();

    [RelayCommand]
    private void AddExpense() => ExpenseEditor.OpenForCreate();

    [RelayCommand]
    private void EditBalance() => BalanceEditor.Open(TotalBalance);

    /// <summary>
    /// Полная перезагрузка данных: приоритет у облака (таймаут 8 секунд),
    /// при ошибке — последний локальный снимок с датой актуальности.
    /// </summary>
    public async Task ReloadAsync()
    {
        if (_isLoading)
        {
            return;
        }

        _isLoading = true;

        // Шаг 1. Мгновенно показываем последний локальный снимок, чтобы UI
        // никогда не висел на «Загрузке...» из-за сети.
        var local = await LoadLocalSnapshotAsync();
        var profile = await _profiles.GetActiveProfileAsync();
        ActiveConnectionText = profile?.Name ?? "Подключение не выбрано";

        if (local is not null)
        {
            local.EnsureIntegrity();
            _data = local;
            ApplyData();
            IsBusy = false;
        }
        else
        {
            IsBusy = true;
        }

        try
        {
            DutyData? cloudData = null;
            string? cloudError = null;

            if (profile is null)
            {
                cloudError = "Не выбрано подключение к облаку.";
            }
            else
            {
                try
                {
                    using var cts = new CancellationTokenSource(CloudWait);
                    cloudData = await _cloud.LoadAsync(profile, cts.Token);
                    if (cloudData is null)
                    {
                        cloudError = "В облачной базе пока нет данных.";
                    }
                }
                catch (Exception ex)
                {
                    cloudError = ex.Message;
                }
            }

            // Шаг 2. Приоритет облака: подменяем снимок, если он пришёл.
            if (cloudData is not null)
            {
                cloudData.EnsureIntegrity();
                _data = cloudData;
                await SaveLocalQuietAsync(_data);

                IsLocalMode = false;
                LocalModeText = string.Empty;
                ShowStatusMessage("Данные загружены из облака", SyncStatusKind.Success);
            }
            else if (local is not null)
            {
                var stamp = FormatStamp(local.UpdatedAt);
                IsLocalMode = true;
                LocalModeText = $"Локальные данные на {stamp}";
                ShowStatusMessage($"Нет связи с облаком. Показаны локальные данные на {stamp}", SyncStatusKind.Warning);
            }
            else
            {
                _data = CreateSeedData();
                await SaveLocalQuietAsync(_data);

                IsLocalMode = profile is not null;
                LocalModeText = IsLocalMode ? "Локальные данные" : string.Empty;
                ShowStatusMessage(cloudError ?? "Созданы новые данные", SyncStatusKind.Warning);
            }

            ApplyData();
        }
        catch (Exception ex)
        {
            _data = CreateSeedData();
            ApplyData();
            await _dialogs.ShowAsync("Не удалось загрузить данные", ex.Message);
        }
        finally
        {
            IsBusy = false;
            _isLoading = false;
        }
    }

    /// <summary>Отправка текущего снимка в активную облачную базу.</summary>
    [RelayCommand]
    private async Task SendDbToFirebaseAsync()
    {
        if (IsSyncing)
        {
            return;
        }

        var profile = await _profiles.GetActiveProfileAsync();
        if (profile is null)
        {
            ShowStatusMessage("Сначала выберите подключение", SyncStatusKind.Warning);
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            "Отправить данные?",
            $"Данные будут записаны в «{profile.Name}».",
            "Отправить",
            "Отмена");

        if (!confirmed)
        {
            return;
        }

        IsSyncing = true;
        try
        {
            _data.UpdatedAt = DateTimeOffset.Now;

            using var cts = new CancellationTokenSource(CloudWait);
            await _cloud.SaveAsync(profile, _data, cts.Token);
            await SaveLocalQuietAsync(_data);

            IsLocalMode = false;
            LocalModeText = string.Empty;
            ShowStatusMessage("Отправлено в облако", SyncStatusKind.Success);
        }
        catch (Exception)
        {
            ShowStatusMessage("Ошибка отправки", SyncStatusKind.Error);
        }
        finally
        {
            IsSyncing = false;
        }
    }

    /// <summary>Удаление всех записей. Баланс не меняется.</summary>
    [RelayCommand]
    private async Task AllDeleteAsync()
    {
        if (_data.Expenses.Count == 0)
        {
            ShowStatusMessage("Записи не найдены", SyncStatusKind.Warning);
            return;
        }

        var count = _data.Expenses.Count;
        var confirmed = await _dialogs.ConfirmAsync(
            "Удалить все записи?",
            $"Будет удалено {count} записей. Баланс не изменится.",
            "Удалить",
            "Отмена");

        if (!confirmed)
        {
            return;
        }

        var candidate = _data.Clone();
        candidate.Expenses.Clear();

        if (!await ApplyChangeAsync(candidate))
        {
            return;
        }

        ShowStatusMessage($"Удалено {count} записей", SyncStatusKind.Success);
    }

    /// <summary>Быстрое удаление траты по иконке корзины на карточке.</summary>
    [RelayCommand]
    private async Task DeleteFastAsync(ExpenseItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            await DeleteExpenseAsync(item.Model.Id);
        }
        catch (Exception ex)
        {
            ShowStatusMessage($"Ошибка удаления: {ex.Message}", SyncStatusKind.Error);
        }
    }

    private void EditExpense(ExpenseItemViewModel item) => ExpenseEditor.OpenForEdit(item.Model);

    private async Task<DutyData?> LoadLocalSnapshotAsync()
    {
        try
        {
            return await _dataService.LoadAsync();
        }
        catch (DataStorageException)
        {
            return null;
        }
    }

    private async Task OnConnectionActivatedAsync(DbConnectionProfile profile)
    {
        ActiveConnectionText = profile.Name;
        await ReloadAsync();
    }

    private IReadOnlyList<ExpenseCategory> GetCategories() => _data.Categories;

    private async Task<bool> SaveCategoryAsync(ExpenseCategory category)
    {
        var candidate = _data.Clone();

        var existing = candidate.Categories.FirstOrDefault(item =>
            string.Equals(item.Key, category.Key, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            candidate.Categories.Add(category.Clone());
        }
        else
        {
            existing.Title = category.Title;
            existing.Icon = category.Icon;
            existing.HexColor = category.HexColor;
        }

        candidate.EnsureIntegrity();
        return await ApplyChangeAsync(candidate);
    }

    private async Task<bool> DeleteCategoryAsync(ExpenseCategory category)
    {
        var candidate = _data.Clone();
        if (candidate.Categories.Count <= 1)
        {
            ShowStatusMessage("Нельзя удалить последнюю категорию", SyncStatusKind.Error);
            return false;
        }

        var target = candidate.Categories.FirstOrDefault(item =>
            string.Equals(item.Key, category.Key, StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            return false;
        }

        candidate.Categories.Remove(target);

        if (target.IsDefault && candidate.Categories.Count > 0)
        {
            candidate.Categories[0].IsDefault = true;
        }

        // EnsureIntegrity переназначит траты удалённой категории на категорию по умолчанию.
        candidate.EnsureIntegrity();
        return await ApplyChangeAsync(candidate);
    }

    private void ApplyData()
    {
        TotalBalance = _data.TotalBalance;

        Expenses.Clear();
        foreach (var expense in _data.Expenses
                     .OrderByDescending(item => item.CreatedAt)
                     .ThenByDescending(item => item.Id))
        {
            var category = _data.GetCategory(expense.CategoryKey);
            Expenses.Add(new ExpenseItemViewModel(expense, category, EditExpense, item => _ = DeleteFastAsync(item)));
        }

        ExpenseEditor.SyncCategories(_data.Categories);
        Categories.Refresh();

        OnPropertyChanged(nameof(HasNoExpenses));
        OnPropertyChanged(nameof(ShowLocalModeHint));
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

        return await ApplyChangeAsync(candidate);
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

        return await ApplyChangeAsync(candidate);
    }

    private async Task<bool> SaveBalanceAsync(decimal amount)
    {
        var candidate = _data.Clone();
        candidate.TotalBalance = amount;

        return await ApplyChangeAsync(candidate);
    }

    /// <summary>
    /// Фиксирует локальное изменение: сохраняет снимок, обновляет экран
    /// и в фоне отправляет данные в облако.
    /// </summary>
    private async Task<bool> ApplyChangeAsync(DutyData candidate)
    {
        candidate.UpdatedAt = DateTimeOffset.Now;

        if (!await TryPersistAsync(candidate))
        {
            return false;
        }

        _data = candidate;
        ApplyData();

        // Отправка не блокирует UI и не мешает работать офлайн.
        _ = PushQuietlyAsync();
        return true;
    }

    private async Task PushQuietlyAsync()
    {
        var profile = await _profiles.GetActiveProfileAsync();
        if (profile is null)
        {
            return;
        }

        try
        {
            using var cts = new CancellationTokenSource(CloudWait);
            await _cloud.SaveAsync(profile, _data, cts.Token);
            IsLocalMode = false;
            LocalModeText = string.Empty;
        }
        catch (Exception)
        {
            IsLocalMode = true;
            LocalModeText = $"Локальные данные на {FormatStamp(_data.UpdatedAt)}";
            ShowStatusMessage("Сохранено локально: облако недоступно", SyncStatusKind.Warning);
        }
    }

    private static string FormatStamp(DateTimeOffset value) =>
        value.ToLocalTime().ToString("dd.MM HH:mm", CultureInfo.InvariantCulture);

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

    private async Task SaveLocalQuietAsync(DutyData data)
    {
        try
        {
            await _dataService.SaveAsync(data);
        }
        catch (DataStorageException)
        {
            // Локальный кэш не критичен: данные уже пришли из облака.
        }
    }

    /// <summary>Показывает статусную плашку и прячет её через несколько секунд.</summary>
    private void ShowStatusMessage(string text, SyncStatusKind kind)
    {
        _statusCts?.Cancel();
        _statusCts?.Dispose();
        _statusCts = new CancellationTokenSource();

        SyncStatusText = text;
        SyncStatusColor = kind switch
        {
            SyncStatusKind.Success => Color.FromArgb("#34C759"),
            SyncStatusKind.Warning => Color.FromArgb("#FF9500"),
            _ => Color.FromArgb("#FF3B30"),
        };
        SyncStatusIcon = kind == SyncStatusKind.Success ? "\uf00c" : "\uf071";

        IsSyncStatusVisible = true;
        _ = HideStatusAfterDelayAsync(_statusCts.Token);
    }

    private async Task HideStatusAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(4), token);
            IsSyncStatusVisible = false;
        }
        catch (OperationCanceledException)
        {
            // Плашку заменили новым сообщением.
        }
    }

    private static DutyData CreateSeedData()
    {
        var data = new DutyData
        {
            TotalBalance = 0,
            UpdatedAt = DateTimeOffset.Now,
            Expenses = new List<Expense>(),
            Categories = CategoryCatalog.CreateDefaults().Select(category => category.Clone()).ToList(),
        };

        data.EnsureIntegrity();
        return data;
    }
}
