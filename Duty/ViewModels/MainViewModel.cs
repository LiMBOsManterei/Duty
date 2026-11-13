using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Duty.Common;
using Duty.Models;
using Duty.Services;

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

    [RelayCommand]
    private void EditBalance() => BalanceEditor.Open(TotalBalance);

    private void EditExpense(ExpenseItemViewModel item) => ExpenseEditor.OpenForEdit(item.Model);

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
            }
            else
            {
                _data = loaded;
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
            Expenses.Add(new ExpenseItemViewModel(expense, EditExpense));
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
