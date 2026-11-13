using System.Globalization;
using System.Text;

namespace Duty.Common;

/// <summary>
/// Форматирование и разбор денежных сумм в русской культуре.
/// </summary>
public static class MoneyFormatter
{
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    /// <summary>Форматирует сумму для отображения: "2 490 ₽" или "2 490,50 ₽".</summary>
    public static string Format(decimal amount) => $"{FormatNumber(amount)} ₽";

    /// <summary>Форматирует сумму без валюты — для поля ввода.</summary>
    public static string FormatPlain(decimal amount) => FormatNumber(amount);

    private static string FormatNumber(decimal amount)
    {
        var format = decimal.Truncate(amount) == amount ? "N0" : "N2";
        return amount.ToString(format, Russian);
    }

    /// <summary>
    /// Разбирает введённую сумму. Поддерживает пробелы, запятую и точку,
    /// символ рубля и числа с копейками.
    /// </summary>
    public static bool TryParse(string? text, bool allowZero, out decimal amount)
    {
        amount = 0m;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var cleaned = new StringBuilder(text.Length);
        foreach (var symbol in text)
        {
            if (char.IsDigit(symbol) || symbol is ',' or '.' or '-')
            {
                cleaned.Append(symbol);
            }
        }

        if (cleaned.Length == 0)
        {
            return false;
        }

        var value = cleaned.ToString();
        var lastComma = value.LastIndexOf(',');
        var lastDot = value.LastIndexOf('.');

        if (lastComma >= 0 && lastDot >= 0)
        {
            // Оба разделителя: тот, что правее, считаем десятичным.
            var decimalSeparator = lastComma > lastDot ? ',' : '.';
            var groupSeparator = decimalSeparator == ',' ? '.' : ',';
            value = value.Replace(groupSeparator.ToString(), string.Empty, StringComparison.Ordinal);
            value = value.Replace(decimalSeparator, '.');
        }
        else if (lastComma >= 0)
        {
            if (value.Count(symbol => symbol == ',') > 1)
            {
                return false;
            }

            value = value.Replace(',', '.');
        }

        if (value.Count(symbol => symbol == '.') > 1)
        {
            return false;
        }

        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
        {
            return false;
        }

        if (amount < 0 || (!allowZero && amount == 0))
        {
            return false;
        }

        return amount <= 1_000_000_000m;
    }

    /// <summary>Округляет сумму до копеек.</summary>
    public static decimal Normalize(decimal amount) =>
        decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}
