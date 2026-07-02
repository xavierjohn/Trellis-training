namespace OrderManagement.Api.v2026_11_12.Models;

using OrderManagement.Domain;

/// <summary>Product response model.</summary>
public record ProductResponse
{
    public Guid Id { get; init; }
    public string ProductName { get; init; } = null!;
    public string Sku { get; init; } = null!;
    public decimal UnitPrice { get; init; }
    public int StockQuantity { get; init; }

    public static ProductResponse From(Product product) => new()
    {
        Id = product.Id,
        ProductName = product.ProductName,
        Sku = product.Sku,
        UnitPrice = product.UnitPrice,
        StockQuantity = product.StockQuantity,
    };
}

/// <summary>Request model for creating a product.</summary>
public record CreateProductRequest
{
    public ProductName ProductName { get; init; } = null!;
    public Sku Sku { get; init; } = null!;
    public UnitPrice UnitPrice { get; init; } = null!;
}

/// <summary>Request model for adding stock to a product.</summary>
public record AddStockRequest
{
    public StockQuantity Quantity { get; init; } = null!;
}
