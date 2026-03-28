namespace OrderManagement.Api.v2026_11_12.Models;

using OrderManagement.Domain;

/// <summary>
/// Response model for a product.
/// </summary>
public record ProductResponse
{
    /// <summary>Product identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Name of the product.</summary>
    public string ProductName { get; init; } = null!;

    /// <summary>Stock keeping unit code.</summary>
    public string Sku { get; init; } = null!;

    /// <summary>Unit price of the product.</summary>
    public MoneyResponse UnitPrice { get; init; } = null!;

    /// <summary>Current stock quantity available.</summary>
    public int StockQuantity { get; init; }

    /// <summary>Maps from domain model to response.</summary>
    public static ProductResponse From(Product product) => new()
    {
        Id = product.Id.Value,
        ProductName = product.ProductName.Value,
        Sku = product.Sku.Value,
        UnitPrice = new MoneyResponse { Amount = product.UnitPrice.Amount, Currency = product.UnitPrice.Currency.Value },
        StockQuantity = product.StockQuantity.Value
    };
}

/// <summary>
/// Response model representing a monetary value.
/// </summary>
public record MoneyResponse
{
    /// <summary>Monetary amount.</summary>
    public decimal Amount { get; init; }

    /// <summary>Currency code.</summary>
    public string Currency { get; init; } = null!;
}
