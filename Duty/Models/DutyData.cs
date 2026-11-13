namespace Duty.Models;

/// <summary>Корень JSON-файла приложения.</summary>
public sealed class DutyData
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public decimal TotalBalance { get; set; }

    public List<Expense> Expenses { get; set; } = new();

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>Глубокая копия — используется для транзакционного сохранения.</summary>
    public DutyData Clone() => new()
    {
        SchemaVersion = SchemaVersion,
        TotalBalance = TotalBalance,
        UpdatedAt = UpdatedAt,
        Expenses = Expenses.Select(expense => expense.Clone()).ToList(),
    };
}
