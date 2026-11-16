using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Duty.Common;
using Duty.Models;
using Microsoft.Maui.Graphics;

namespace Duty.ViewModels;

/// <summary>Элемент сетки «Недавние траты».</summary>
public sealed class ExpenseItemViewModel
{
    public ExpenseItemViewModel(Expense expense, Action<ExpenseItemViewModel> edit)
    {
        Model = expense;
        Category = ExpenseCategories.Get(expense.CategoryKey);
        EditCommand = new RelayCommand(() => edit(this));
        FastDeleteCommand = new RelayCommand(() => edit(this));
    }
    public ExpenseItemViewModel(Expense expense, Action<ExpenseItemViewModel> edit, Action<ExpenseItemViewModel> delete)
    {
        Model = expense;
        Category = ExpenseCategories.Get(expense.CategoryKey);
        EditCommand = new RelayCommand(() => edit(this));
        FastDeleteCommand = new RelayCommand(() => delete(this));
    }


    public Expense Model { get; }

    public ExpenseCategory Category { get; }

    public string Name => Model.Name;

    public string FormattedAmount => MoneyFormatter.Format(Model.Amount);

    public string Icon => Category.Icon;

    public Color IconColor => Color.FromArgb(Category.HexColor);

    public string CategoryTitle => Category.Title;

    public ICommand EditCommand { get; }
    
    public ICommand FastDeleteCommand { get; }

    public string AccessibilityDescription =>
        $"{Name}, {FormattedAmount}, категория {CategoryTitle}. Нажмите, чтобы изменить.";
}
