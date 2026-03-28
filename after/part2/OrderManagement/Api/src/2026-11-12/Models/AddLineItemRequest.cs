namespace OrderManagement.Api.v2026_11_12.Models;

using OrderManagement.Domain;

/// <summary>
/// Request model for adding a line item to a draft order.
/// </summary>
public record AddLineItemRequest
{
    /// <summary>Product identifier for the line item.</summary>
    public ProductId ProductId { get; init; } = null!;

    /// <summary>Quantity of the product to add.</summary>
    public LineItemQuantity Quantity { get; init; } = null!;
}
