namespace Duty.Models;

/// <summary>Корень JSON-файла приложения и объект, который уходит в облако.</summary>
public sealed class DutyData
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public decimal TotalBalance { get; set; }

    public List<Expense> Expenses { get; set; } = new();

    /// <summary>Пользовательские категории. Пустой список заменяется набором по умолчанию.</summary>
    public List<ExpenseCategory> Categories { get; set; } = new();

    /// <summary>Момент последнего изменения данных. Используется как «актуально на».</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>Глубокая копия — используется для транзакционного сохранения.</summary>
    public DutyData Clone() => new()
    {
        SchemaVersion = SchemaVersion,
        TotalBalance = TotalBalance,
        UpdatedAt = UpdatedAt,
        Expenses = Expenses.Select(expense => expense.Clone()).ToList(),
        Categories = Categories.Select(category => category.Clone()).ToList(),
    };

    /// <summary>
    /// Категория по умолчанию (первая помеченная IsDefault либо первая в списке).
    /// Метод, а не свойство: вычисляемое значение не должно попадать в JSON и облако.
    /// </summary>
    public ExpenseCategory GetDefaultCategory() =>
        Categories.FirstOrDefault(category => category.IsDefault)
        ?? Categories.FirstOrDefault()
        ?? CategoryCatalog.CreateDefaults()[^1];

    /// <summary>Находит категорию по ключу; неизвестный ключ даёт категорию по умолчанию.</summary>
    public ExpenseCategory GetCategory(string? key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            foreach (var category in Categories)
            {
                if (string.Equals(category.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return category;
                }
            }
        }

        return GetDefaultCategory();
    }

    /// <summary>
    /// Приводит данные к согласованному виду: категории, ссылки трат, идентификаторы,
    /// названия и суммы. Отрицательный баланс намеренно не ограничивается.
    /// </summary>
    public void EnsureIntegrity()
    {
        SchemaVersion = CurrentSchemaVersion;
        Expenses ??= new List<Expense>();
        Categories ??= new List<ExpenseCategory>();

        NormalizeCategories();
        NormalizeExpenses();
    }

    private void NormalizeCategories()
    {
        var cleaned = new List<ExpenseCategory>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var category in Categories)
        {
            if (category is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(category.Key))
            {
                category.Key = Guid.NewGuid().ToString("N");
            }

            if (!keys.Add(category.Key))
            {
                continue;
            }

            category.Title = string.IsNullOrWhiteSpace(category.Title) ? "Без названия" : category.Title.Trim();
            category.Icon = string.IsNullOrWhiteSpace(category.Icon) ? CategoryCatalog.FallbackIcon : category.Icon;
            category.HexColor = ExpenseCategory.NormalizeColor(category.HexColor);
            cleaned.Add(category);
        }

        if (cleaned.Count == 0)
        {
            foreach (var preset in CategoryCatalog.CreateDefaults())
            {
                cleaned.Add(preset);
                keys.Add(preset.Key);
            }
        }

        var defaults = cleaned.Where(category => category.IsDefault).ToList();
        if (defaults.Count == 0)
        {
            cleaned[0].IsDefault = true;
        }
        else
        {
            // По умолчанию может быть только одна категория.
            foreach (var extra in defaults.Skip(1))
            {
                extra.IsDefault = false;
            }
        }

        Categories = cleaned;
    }

    private void NormalizeExpenses()
    {
        var ids = new HashSet<Guid>();
        var defaultKey = GetDefaultCategory().Key;

        foreach (var expense in Expenses)
        {
            if (expense is null)
            {
                continue;
            }

            if (expense.Id == Guid.Empty || !ids.Add(expense.Id))
            {
                expense.Id = Guid.NewGuid();
                ids.Add(expense.Id);
            }

            expense.Name = string.IsNullOrWhiteSpace(expense.Name) ? "Без названия" : expense.Name.Trim();
            if (expense.Amount < 0)
            {
                expense.Amount = Math.Abs(expense.Amount);
            }

            if (!Categories.Any(category => string.Equals(category.Key, expense.CategoryKey, StringComparison.OrdinalIgnoreCase)))
            {
                expense.CategoryKey = defaultKey;
            }

            if (expense.CreatedAt == default)
            {
                expense.CreatedAt = DateTimeOffset.Now;
            }
        }

        // Убираем возможные null-элементы после десериализации.
        Expenses = Expenses.Where(expense => expense is not null).ToList();
    }
}
