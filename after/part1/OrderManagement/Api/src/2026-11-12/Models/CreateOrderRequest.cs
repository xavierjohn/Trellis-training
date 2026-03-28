namespace OrderManagement.Api.v2026_11_12.Models;

using OrderManagement.Domain;

/// <summary>
/// A single line item in a create-order request.
/// </summary>
public record CreateOrderLineItemRequest
{
    /// <summary>Product identifier.</summary>
    public ProductId ProductId { get; init; } = null!;
    /// <summary>Quantity to order (1–999).</summary>
    public LineItemQuantity Quantity { get; init; } = null!;
}

/// <summary>
/// Request model for creating a draft order.
/// Validated via <c>CreateDraftOrderCommand.TryCreate</c> in the controller.
/// </summary>
public record CreateOrderRequest
{
    /// <summary>Customer placing the order.</summary>
    public CustomerId CustomerId { get; init; } = null!;
    /// <summary>Line items. Must be non-empty with no duplicate product IDs.</summary>
    public IReadOnlyList<CreateOrderLineItemRequest> LineItems { get; init; } = [];
}
