namespace OrderManagement.Api.v2026_11_12.Models;

using OrderManagement.Domain;

/// <summary>
/// Request model for adding stock to a product.
/// </summary>
public record AddStockRequest
{
    /// <summary>Quantity of stock to add.</summary>
    public StockQuantity Quantity { get; init; } = null!;
}
