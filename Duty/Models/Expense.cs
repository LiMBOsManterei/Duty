namespace Duty.Models;

/// <summary>Одна трата.</summary>
public sealed class Expense
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    /// <summary>Ключ категории. Неизвестный ключ при загрузке заменится на категорию по умолчанию.</summary>
    public string CategoryKey { get; set; } = "other";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public Expense Clone() => new()
    {
        Id = Id,
        Name = Name,
        Amount = Amount,
        CategoryKey = CategoryKey,
        CreatedAt = CreatedAt,
    };
}
