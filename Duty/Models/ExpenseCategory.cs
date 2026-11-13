namespace Duty.Models;

/// <summary>
/// Категория траты: ключ для JSON, подпись, иконка Font Awesome и цвет.
/// </summary>
public sealed record ExpenseCategory(string Key, string Title, string Icon, string HexColor);

/// <summary>Справочник категорий приложения.</summary>
public static class ExpenseCategories
{
    public static IReadOnlyList<ExpenseCategory> All { get; } = new[]
    {
        new ExpenseCategory("groceries", "Продукты", "\uf07a", "#34C759"),
        new ExpenseCategory("cafe", "Кафе", "\uf7b6", "#FF9500"),
        new ExpenseCategory("transport", "Транспорт", "\uf1b9", "#007AFF"),
        new ExpenseCategory("housing", "Жильё", "\uf015", "#5856D6"),
        new ExpenseCategory("health", "Здоровье", "\uf21e", "#FF3B30"),
        new ExpenseCategory("entertainment", "Развлечения", "\uf008", "#FF2D55"),
        new ExpenseCategory("clothes", "Одежда", "\uf553", "#00C7BE"),
        new ExpenseCategory("communication", "Связь", "\uf3cd", "#5E5CE6"),
        new ExpenseCategory("education", "Образование", "\uf19d", "#AF52DE"),
        new ExpenseCategory("other", "Другое", "\uf141", "#8E8E93"),
    };

    public static ExpenseCategory Default => All[^1];

    public static ExpenseCategory Get(string? key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            foreach (var category in All)
            {
                if (string.Equals(category.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return category;
                }
            }
        }

        return Default;
    }
}
