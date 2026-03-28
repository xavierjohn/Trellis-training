namespace OrderManagement.Api.v2026_11_12.Models;

using OrderManagement.Domain;

/// <summary>
/// Response model for a product.
/// </summary>
public record ProductResponse
{
    /// <summary>Unique product identifier.</summary>
    public Guid Id { get; init; }
    /// <summary>Display name of the product.</summary>
    public string ProductName { get; init; } = null!;
    /// <summary>Stock keeping unit code.</summary>
    public string Sku { get; init; } = null!;
    /// <summary>Unit price.</summary>
    public MoneyDto UnitPrice { get; init; } = null!;
    /// <summary>Current available stock quantity.</summary>
    public int StockQuantity { get; init; }

    /// <summary>Maps from domain aggregate.</summary>
    public static ProductResponse From(Product p) => new()
    {
        Id = p.Id.Value,
        ProductName = p.ProductName.Value,
        Sku = p.Sku.Value,
        UnitPrice = MoneyDto.From(p.UnitPrice),
        StockQuantity = p.StockQuantity.Value
    };
}
