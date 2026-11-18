using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Duty.Models;
using Duty.Services;
using Microsoft.Maui.Graphics;

namespace Duty.ViewModels;

/// <summary>Строка списка категорий.</summary>
public sealed class CategoryItemViewModel
{
    public CategoryItemViewModel(
        ExpenseCategory model,
        Action<CategoryItemViewModel> edit,
        Action<CategoryItemViewModel> delete)
    {
        Model = model;
        EditCommand = new RelayCommand(() => edit(this));
        DeleteCommand = new RelayCommand(() => delete(this));
    }

    public ExpenseCategory Model { get; }

    public string Title => Model.Title;

    public string Icon => Model.Icon;

    public Color IconColor => Color.FromArgb(Model.HexColor);

    public bool IsDefault => Model.IsDefault;

    public ICommand EditCommand { get; }

    public ICommand DeleteCommand { get; }
}

/// <summary>Плитка выбора цвета в редакторе категории.</summary>
public partial class ColorOptionViewModel : ObservableObject
{
    private static readonly Brush SelectedBorder = new SolidColorBrush(Color.FromArgb("#1A1A1A"));
    private static readonly Brush NormalBorder = new SolidColorBrush(Color.FromArgb("#E5E5EA"));

    public ColorOptionViewModel(string hex, Action<ColorOptionViewModel> select)
    {
        Hex = hex;
        SelectCommand = new RelayCommand(() => select(this));
    }

    public string Hex { get; }

    public Color Swatch => Color.FromArgb(Hex);

    public ICommand SelectCommand { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public Brush BorderBrush => IsSelected ? SelectedBorder : NormalBorder;

    public double BorderWidth => IsSelected ? 2 : 1;

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(BorderBrush));
        OnPropertyChanged(nameof(BorderWidth));
    }
}

/// <summary>Плитка выбора иконки Font Awesome в редакторе категории.</summary>
public partial class IconOptionViewModel : ObservableObject
{
    public IconOptionViewModel(string icon, Action<IconOptionViewModel> select)
    {
        Icon = icon;
        SelectCommand = new RelayCommand(() => select(this));
    }

    public string Icon { get; }

    public ICommand SelectCommand { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public Color BackgroundColor => IsSelected ? Color.FromArgb("#1C1C1E") : Color.FromArgb("#F2F2F7");

    public Color ForegroundColor => IsSelected ? Colors.White : Color.FromArgb("#1A1A1A");

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(ForegroundColor));
    }
}

/// <summary>
/// Управление категориями: список, создание, редактирование и удаление.
/// Сами изменения применяет MainViewModel — здесь только UI-состояние.
/// </summary>
public partial class CategoriesViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly Func<IReadOnlyList<ExpenseCategory>> _getCategories;
    private readonly Func<ExpenseCategory, Task<bool>> _saveCategory;
    private readonly Func<ExpenseCategory, Task<bool>> _deleteCategory;

    private string? _editingKey;
    private bool _editingIsDefault;

    public CategoriesViewModel(
        IDialogService dialogs,
        Func<IReadOnlyList<ExpenseCategory>> getCategories,
        Func<ExpenseCategory, Task<bool>> saveCategory,
        Func<ExpenseCategory, Task<bool>> deleteCategory)
    {
        _dialogs = dialogs;
        _getCategories = getCategories;
        _saveCategory = saveCategory;
        _deleteCategory = deleteCategory;

        PaletteColors = new ObservableCollection<ColorOptionViewModel>(
            CategoryCatalog.Colors.Select(hex => new ColorOptionViewModel(hex, SelectColor)));

        PaletteIcons = new ObservableCollection<IconOptionViewModel>(
            CategoryCatalog.Icons.Select(icon => new IconOptionViewModel(icon, SelectIcon)));

        EditorTitle = "Новая категория";
        EditorName = string.Empty;
        SelectedColorHex = CategoryCatalog.Colors[0];
        SelectedIcon = CategoryCatalog.Icons[0];
    }

    public ObservableCollection<CategoryItemViewModel> Items { get; } = new();

    public ObservableCollection<ColorOptionViewModel> PaletteColors { get; }

    public ObservableCollection<IconOptionViewModel> PaletteIcons { get; }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial bool IsEditorOpen { get; set; }

    [ObservableProperty]
    public partial string EditorTitle { get; set; }

    [ObservableProperty]
    public partial string EditorName { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEditorError))]
    public partial string? EditorError { get; set; }

    [ObservableProperty]
    public partial bool IsEditing { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewColor))]
    public partial string SelectedColorHex { get; set; }

    [ObservableProperty]
    public partial string SelectedIcon { get; set; }

    public bool HasEditorError => !string.IsNullOrWhiteSpace(EditorError);

    public Color PreviewColor => Color.FromArgb(SelectedColorHex);

    /// <summary>Открывает панель управления категориями.</summary>
    [RelayCommand]
    private async Task OpenAsync()
    {
        await RefreshAsync();
        IsOpen = true;
    }

    [RelayCommand]
    private void Close()
    {
        IsOpen = false;
        IsEditorOpen = false;
        EditorError = null;
    }

    /// <summary>Перечитывает список из актуальных данных.</summary>
    public void Refresh()
    {
        Items.Clear();
        foreach (var category in _getCategories())
        {
            Items.Add(new CategoryItemViewModel(category, EditCategory, item => _ = DeleteAsync(item)));
        }
    }

    private Task RefreshAsync()
    {
        Refresh();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void AddCategory()
    {
        _editingKey = null;
        _editingIsDefault = false;
        IsEditing = false;
        EditorTitle = "Новая категория";
        EditorName = string.Empty;
        EditorError = null;
        SelectColor(PaletteColors[0]);
        SelectIcon(PaletteIcons[0]);
        IsEditorOpen = true;
    }

    private void EditCategory(CategoryItemViewModel item)
    {
        _editingKey = item.Model.Key;
        _editingIsDefault = item.Model.IsDefault;
        IsEditing = true;
        EditorTitle = "Изменить категорию";
        EditorName = item.Model.Title;
        EditorError = null;

        var color = PaletteColors.FirstOrDefault(option =>
                        string.Equals(option.Hex, item.Model.HexColor, StringComparison.OrdinalIgnoreCase))
                    ?? PaletteColors[0];
        SelectColor(color);

        var icon = PaletteIcons.FirstOrDefault(option => option.Icon == item.Model.Icon)
                   ?? PaletteIcons[0];
        SelectIcon(icon);

        IsEditorOpen = true;
    }

    [RelayCommand]
    private void CloseEditor()
    {
        IsEditorOpen = false;
        EditorError = null;
    }

    [RelayCommand]
    private async Task SaveEditorAsync()
    {
        if (string.IsNullOrWhiteSpace(EditorName))
        {
            EditorError = "Укажите название категории.";
            return;
        }

        var category = new ExpenseCategory
        {
            Key = _editingKey ?? Guid.NewGuid().ToString("N"),
            Title = EditorName.Trim(),
            Icon = SelectedIcon,
            HexColor = SelectedColorHex,
            IsDefault = _editingIsDefault,
        };

        if (await _saveCategory(category))
        {
            Refresh();
            IsEditorOpen = false;
            EditorError = null;

            // Если категория новая — сразу возвращаемся к списку.
            IsOpen = true;
        }
        else
        {
            EditorError = "Не удалось сохранить категорию.";
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(CategoryItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (Items.Count <= 1)
        {
            await _dialogs.ShowAsync("Нельзя удалить", "В приложении должна остаться хотя бы одна категория.");
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            "Удалить категорию?",
            $"«{item.Title}» будет удалена. Траты этой категории перейдут в категорию по умолчанию.",
            "Удалить",
            "Отмена");

        if (!confirmed)
        {
            return;
        }

        if (await _deleteCategory(item.Model))
        {
            Refresh();
        }
    }

    private void SelectColor(ColorOptionViewModel option)
    {
        SelectedColorHex = option.Hex;
        foreach (var color in PaletteColors)
        {
            color.IsSelected = ReferenceEquals(color, option);
        }
    }

    private void SelectIcon(IconOptionViewModel option)
    {
        SelectedIcon = option.Icon;
        foreach (var icon in PaletteIcons)
        {
            icon.IsSelected = ReferenceEquals(icon, option);
        }
    }
}
