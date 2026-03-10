namespace OrderManagement.Api.v2026_11_12.Models;

using OrderManagement.Domain.Aggregates;
using OrderManagement.Domain.ValueObjects;

public record ProductResponse(
    Guid Id,
    string ProductName,
    string Sku,
    decimal UnitPrice,
    string UnitPriceCurrency,
    int StockQuantity)
{
    public static ProductResponse From(Product product) => new(
        product.Id.Value,
        product.ProductName.Value,
        product.Sku.Value,
        product.UnitPrice.Amount,
        product.UnitPrice.Currency.Value,
        product.StockQuantity.Value);
}

public record CreateProductRequest
{
    public ProductName ProductName { get; init; } = null!;
    public Sku Sku { get; init; } = null!;
    public decimal UnitPrice { get; init; }
}

public record AddStockRequest
{
    public int Quantity { get; init; }
}
