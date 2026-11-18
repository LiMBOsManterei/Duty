namespace Duty.Models;

/// <summary>
/// Категория траты: ключ для JSON, подпись, иконка Font Awesome и цвет.
/// Хранится внутри <see cref="DutyData"/> и синхронизируется с облаком.
/// </summary>
public sealed class ExpenseCategory
{
    public string Key { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>Код иконки Font Awesome, например "\uf07a".</summary>
    public string Icon { get; set; } = CategoryCatalog.FallbackIcon;

    /// <summary>Цвет в формате #RRGGBB.</summary>
    public string HexColor { get; set; } = CategoryCatalog.FallbackColor;

    /// <summary>Категория, на которую переназначаются траты при удалении.</summary>
    public bool IsDefault { get; set; }

    public ExpenseCategory Clone() => new()
    {
        Key = Key,
        Title = Title,
        Icon = Icon,
        HexColor = HexColor,
        IsDefault = IsDefault,
    };

    /// <summary>Создаёт новую категорию со сгенерированным ключом.</summary>
    public static ExpenseCategory Create(string title, string icon, string hexColor) => new()
    {
        Key = Guid.NewGuid().ToString("N"),
        Title = title,
        Icon = icon,
        HexColor = hexColor,
    };

    /// <summary>Проверяет строку цвета и приводит её к виду #RRGGBB.</summary>
    public static string NormalizeColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return CategoryCatalog.FallbackColor;
        }

        var text = value.Trim();
        if (!text.StartsWith('#'))
        {
            text = "#" + text;
        }

        return text.Length is 7 or 9 ? text.ToUpperInvariant() : CategoryCatalog.FallbackColor;
    }
}

/// <summary>Справочные наборы для выбора иконки и цвета, а также категории по умолчанию.</summary>
public static class CategoryCatalog
{
    public const string FallbackIcon = "\uf141";
    public const string FallbackColor = "#8E8E93";

    /// <summary>Палитра цветов для редактора категорий.</summary>
    public static IReadOnlyList<string> Colors { get; } = new[]
    {
        "#FF3B30", "#FF9500", "#FFCC00", "#34C759", "#00C7BE",
        "#32ADE6", "#007AFF", "#5856D6", "#AF52DE", "#FF2D55",
        "#C4A484", "#E5B84B", "#005BFF", "#1A1A1A", "#8E8E93",
    };

    /// <summary>Набор иконок Font Awesome, доступных для категорий.</summary>
    public static IReadOnlyList<string> Icons { get; } = new[]
    {
        "\uf07a", // cart-shopping
        "\uf06b", // gift
        "\uf52f", // gas-pump
        "\uf805", // burger
        "\uf0ad", // wrench
        "\uf015", // house
        "\uf1b9", // car
        "\uf7b6", // mug-hot
        "\uf21e", // heart-pulse
        "\uf008", // film
        "\uf553", // shirt
        "\uf3cd", // mobile-screen-button
        "\uf19d", // graduation-cap
        "\uf1c0", // database
        "\uf0c2", // cloud
        "\uf0e7", // bolt
        "\uf1d8", // paper-plane
        "\uf0f3", // bell
        "\uf1b2", // cube
        "\uf0d6", // money-bill
        "\uf158", // ruble-sign
        "\uf141", // ellipsis
    };

    /// <summary>Категории, которые создаются при первом запуске.</summary>
    public static IReadOnlyList<ExpenseCategory> CreateDefaults() => new[]
    {
        new ExpenseCategory { Key = "groceries", Title = "Продукты",  Icon = "\uf07a", HexColor = "#8E8E93" },
        new ExpenseCategory { Key = "ozon",      Title = "Озон",      Icon = "\uf06b", HexColor = "#005BFF" },
        new ExpenseCategory { Key = "fuel",      Title = "Топливо",   Icon = "\uf52f", HexColor = "#E5B84B" },
        new ExpenseCategory { Key = "cafe",      Title = "Питание",   Icon = "\uf805", HexColor = "#C4A484" },
        new ExpenseCategory { Key = "transport", Title = "Транспорт", Icon = "\uf0ad", HexColor = "#8E8E93" },
        new ExpenseCategory { Key = "other",     Title = "Другое",    Icon = "\uf141", HexColor = "#8E8E93", IsDefault = true },
    };
}
