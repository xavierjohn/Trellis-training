namespace OrderManagement.Api.v2026_11_12.Models;

using OrderManagement.Domain;

/// <summary>
/// Request model for creating a draft order.
/// </summary>
public record CreateDraftOrderRequest
{
    /// <summary>Customer identifier for the order.</summary>
    public CustomerId CustomerId { get; init; } = null!;

    /// <summary>Line items to include in the draft order.</summary>
    public List<CreateOrderLineItemRequest> LineItems { get; init; } = [];
}

/// <summary>
/// Request model for a line item within a draft order.
/// </summary>
public record CreateOrderLineItemRequest
{
    /// <summary>Product identifier for the line item.</summary>
    public ProductId ProductId { get; init; } = null!;

    /// <summary>Quantity of the product to order.</summary>
    public LineItemQuantity Quantity { get; init; } = null!;
}
