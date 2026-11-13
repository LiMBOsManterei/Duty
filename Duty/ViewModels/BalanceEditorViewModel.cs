using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Duty.Common;

namespace Duty.ViewModels;

/// <summary>Состояние и команды диалога изменения общего баланса.</summary>
public partial class BalanceEditorViewModel : ObservableObject
{
    private readonly Func<decimal, Task<bool>> _save;

    public BalanceEditorViewModel(Func<decimal, Task<bool>> save)
    {
        _save = save;
        AmountText = string.Empty;
    }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string AmountText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool IsSaving { get; set; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsNotSaving => !IsSaving;

    public void Open(decimal currentBalance)
    {
        AmountText = MoneyFormatter.FormatPlain(currentBalance);
        ErrorMessage = null;
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

        if (!MoneyFormatter.TryParse(AmountText, allowZero: true, out var amount))
        {
            ErrorMessage = "Введите корректный баланс, например 150 000.";
            return;
        }

        IsSaving = true;
        try
        {
            if (await _save(MoneyFormatter.Normalize(amount)))
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

    partial void OnAmountTextChanged(string value)
    {
        if (HasError)
        {
            ErrorMessage = null;
        }
    }

    partial void OnIsSavingChanged(bool value) => OnPropertyChanged(nameof(IsNotSaving));
}
