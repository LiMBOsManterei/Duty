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
           new ExpenseCategory("groceries", "Продукты",  "\uf07a", "#8E8E93"), // Системный зеленый
           new ExpenseCategory("ozon",      "Озон",      "\uf06b", "#005BFF"), // Фирменный голубой/синий Ozon (или "#32ADE6" для более светлого голубого)
           new ExpenseCategory("fuel",      "Топливо",   "\uf52f", "#E5B84B"), // Приглушенный пастельно-желтый / горчичный
           new ExpenseCategory("cafe",      "Питание",   "\uf805", "#C4A484"), // Теплый светло-коричневый / бежевый
           new ExpenseCategory("transport", "Транспорт", "\uf0ad", "#8E8E93"), // Системный зеленый, как у продуктов
           new ExpenseCategory("other",     "Другое",    "\uf141", "#8E8E93"), // Системный серый
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
