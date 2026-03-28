namespace OrderManagement.Api.v2026_11_12.Models;

using Trellis.Primitives;

/// <summary>
/// Money shape used in both request and response DTOs.
/// </summary>
public record MoneyDto
{
    /// <summary>Monetary amount.</summary>
    public decimal Amount { get; init; }
    /// <summary>ISO 4217 currency code (e.g., "USD").</summary>
    public string Currency { get; init; } = "USD";

    /// <summary>Maps from domain Money.</summary>
    public static MoneyDto From(Money money) => new()
    {
        Amount = money.Amount,
        Currency = (string)money.Currency
    };
}
