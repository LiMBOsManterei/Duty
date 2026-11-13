using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Duty.Models;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Duty.ViewModels;

/// <summary>Чип выбора категории в форме траты.</summary>
public partial class CategoryOptionViewModel : ObservableObject
{
    private static readonly Color SelectedBackground = Color.FromArgb("#1C1C1E");
    private static readonly Color UnselectedBackground = Color.FromArgb("#F7F8FA");
    private static readonly Color SelectedForeground = Colors.White;
    private static readonly Color UnselectedForeground = Color.FromArgb("#1A1A1A");
    private static readonly Brush SelectedBorderBrush = new SolidColorBrush(Colors.Transparent);
    private static readonly Brush UnselectedBorderBrush = new SolidColorBrush(Color.FromArgb("#E5E5EA"));

    public CategoryOptionViewModel(ExpenseCategory category, Action<CategoryOptionViewModel> select)
    {
        Category = category;
        SelectCommand = new RelayCommand(() => select(this));
    }

    public ExpenseCategory Category { get; }

    public string Key => Category.Key;

    public string Title => Category.Title;

    public string Icon => Category.Icon;

    public ICommand SelectCommand { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public Color BackgroundColor => IsSelected ? SelectedBackground : UnselectedBackground;

    public Color ForegroundColor => IsSelected ? SelectedForeground : UnselectedForeground;

    public Brush BorderBrush => IsSelected ? SelectedBorderBrush : UnselectedBorderBrush;

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(ForegroundColor));
        OnPropertyChanged(nameof(BorderBrush));
    }
}
