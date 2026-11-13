using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Duty.Common;
using Duty.Models;
using Duty.Services;

namespace Duty.ViewModels;

/// <summary>
/// Состояние и команды нижней формы создания/редактирования траты.
/// </summary>
public partial class ExpenseEditorViewModel : ObservableObject
{
    private readonly Func<ExpenseDraft, Task<bool>> _save;
    private readonly Func<Guid, Task<bool>> _delete;
    private readonly IDialogService _dialogs;
    private Guid? _editingId;

    public ExpenseEditorViewModel(
        Func<ExpenseDraft, Task<bool>> save,
        Func<Guid, Task<bool>> delete,
        IDialogService dialogs)
    {
        _save = save;
        _delete = delete;
        _dialogs = dialogs;

        Categories = new ObservableCollection<CategoryOptionViewModel>(
            ExpenseCategories.All.Select(category => new CategoryOptionViewModel(category, SelectCategory)));

        Title = "Новая трата";
        Name = string.Empty;
        AmountText = string.Empty;
        SelectedCategoryKey = ExpenseCategories.Default.Key;
    }

    public ObservableCollection<CategoryOptionViewModel> Categories { get; }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string AmountText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool IsEditing { get; set; }

    [ObservableProperty]
    public partial string SelectedCategoryKey { get; set; }

    [ObservableProperty]
    public partial bool IsSaving { get; set; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsNotSaving => !IsSaving;

    public void OpenForCreate()
    {
        _editingId = null;
        IsEditing = false;
        Title = "Новая трата";
        Name = string.Empty;
        AmountText = string.Empty;
        ErrorMessage = null;
        SetSelectedCategory(ExpenseCategories.Default.Key);
        IsOpen = true;
    }

    public void OpenForEdit(Expense expense)
    {
        _editingId = expense.Id;
        IsEditing = true;
        Title = "Изменить трату";
        Name = expense.Name;
        AmountText = MoneyFormatter.FormatPlain(expense.Amount);
        ErrorMessage = null;
        SetSelectedCategory(expense.CategoryKey);
        IsOpen = true;
    }

    [RelayCommand]
    private void Close()
    {
        if (IsSaving)
        {
            return;
        }

        IsOpen = false;
        ErrorMessage = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsSaving)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Укажите название траты.";
            return;
        }

        if (!MoneyFormatter.TryParse(AmountText, allowZero: false, out var amount))
        {
            ErrorMessage = "Введите сумму больше нуля, например 2 490.";
            return;
        }

        var draft = new ExpenseDraft(
            _editingId,
            Name.Trim(),
            MoneyFormatter.Normalize(amount),
            SelectedCategoryKey);

        IsSaving = true;
        try
        {
            if (await _save(draft))
            {
                IsOpen = false;
                ErrorMessage = null;
            }
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (!IsEditing || _editingId is not Guid id || IsSaving)
        {
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            "Удалить трату?",
            "Трата будет удалена, а сумма вернётся на баланс.",
            "Удалить",
            "Отмена");

        if (!confirmed)
        {
            return;
        }

        IsSaving = true;
        try
        {
            if (await _delete(id))
            {
                IsOpen = false;
                ErrorMessage = null;
            }
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void SelectCategory(CategoryOptionViewModel option)
    {
        SelectedCategoryKey = option.Key;
        foreach (var category in Categories)
        {
            category.IsSelected = ReferenceEquals(category, option);
        }
    }

    private void SetSelectedCategory(string key)
    {
        var normalizedKey = ExpenseCategories.Get(key).Key;
        SelectedCategoryKey = normalizedKey;

        foreach (var category in Categories)
        {
            category.IsSelected = string.Equals(
                category.Key,
                normalizedKey,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    partial void OnNameChanged(string value)
    {
        if (HasError)
        {
            ErrorMessage = null;
        }
    }

    partial void OnAmountTextChanged(string value)
    {
        if (HasError)
        {
            ErrorMessage = null;
        }
    }

    partial void OnIsSavingChanged(bool value) => OnPropertyChanged(nameof(IsNotSaving));
}
