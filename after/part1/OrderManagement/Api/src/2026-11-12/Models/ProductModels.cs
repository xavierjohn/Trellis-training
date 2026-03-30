#pragma warning disable CS1591
namespace OrderManagement.Api.v2026_11_12.Models;

using OrderManagement.Domain;
using Trellis.Primitives;

public record CreateProductRequest
{
    public ProductName ProductName { get; init; } = null!;
    public Sku Sku { get; init; } = null!;
    public Money UnitPrice { get; init; } = null!;
}

public record AddStockRequest
{
    public StockQuantity Quantity { get; init; } = null!;
}

public record ProductResponse
{
    public Guid Id { get; init; }
    public string ProductName { get; init; } = null!;
    public string Sku { get; init; } = null!;
    public Money UnitPrice { get; init; } = null!;
    public int StockQuantity { get; init; }

    public static ProductResponse From(Product product) => new()
    {
        Id = product.Id.Value,
        ProductName = product.ProductName.Value,
        Sku = product.Sku.Value,
        UnitPrice = product.UnitPrice,
        StockQuantity = product.StockQuantity.Value
    };
}
