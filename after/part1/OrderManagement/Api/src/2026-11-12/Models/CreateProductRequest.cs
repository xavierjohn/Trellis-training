namespace OrderManagement.Api.v2026_11_12.Models;

using OrderManagement.Domain;

/// <summary>
/// Request model for creating a product.
/// </summary>
public record CreateProductRequest
{
    /// <summary>Display name of the product.</summary>
    public ProductName ProductName { get; init; } = null!;
    /// <summary>Stock keeping unit code.</summary>
    public Sku Sku { get; init; } = null!;

    /// <summary>Unit price with amount and currency. Converted to Money via TryCreate in controller.</summary>
    public MoneyDto UnitPrice { get; init; } = null!;
}
