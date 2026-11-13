namespace Duty.Models;

/// <summary>
/// Данные из формы траты. Id == null означает создание новой записи.
/// </summary>
public sealed record ExpenseDraft(Guid? Id, string Name, decimal Amount, string CategoryKey);
