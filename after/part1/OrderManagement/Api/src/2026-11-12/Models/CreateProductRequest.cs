namespace OrderManagement.Api.v2026_11_12.Models;

using OrderManagement.Domain;
using Trellis.Primitives;

/// <summary>
/// Request model for creating a product.
/// </summary>
public record CreateProductRequest
{
    /// <summary>Name of the product.</summary>
    public ProductName ProductName { get; init; } = null!;

    /// <summary>Stock keeping unit code.</summary>
    public Sku Sku { get; init; } = null!;

    /// <summary>Unit price of the product.</summary>
    public Money UnitPrice { get; init; } = null!;
}
