namespace OrderManagement.Domain;

using Trellis.Primitives;

/// <summary>
/// Input data for creating a line item when constructing or modifying an order.
/// </summary>
public readonly record struct OrderLineItemInput(
    ProductId ProductId,
    ProductName ProductName,
    LineItemQuantity Quantity,
    Money UnitPrice);
