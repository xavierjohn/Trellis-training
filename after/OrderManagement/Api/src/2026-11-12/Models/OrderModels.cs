namespace OrderManagement.Api.v2026_11_12.Models;

using OrderManagement.Domain.Aggregates;
using OrderManagement.Domain.ValueObjects;

public record OrderResponse(
    Guid Id,
    Guid CustomerId,
    string CreatedByActorId,
    string Status,
    List<LineItemResponse> LineItems,
    decimal OrderTotal,
    string OrderTotalCurrency,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    DateTime? ShippedAt)
{
    public static OrderResponse From(Order order) => new(
        order.Id.Value,
        order.CustomerId.Value,
        order.CreatedByActorId.Value,
        order.Status.Name,
        order.LineItems.Select(LineItemResponse.From).ToList(),
        order.CalculateTotal().Amount,
        order.CalculateTotal().Currency.Value,
        order.CreatedAt,
        order.SubmittedAt.Match(v => (DateTime?)v, () => null),
        order.ShippedAt.Match(v => (DateTime?)v, () => null));
}

public record LineItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    string UnitPriceCurrency)
{
    public static LineItemResponse From(LineItem lineItem) => new(
        lineItem.Id.Value,
        lineItem.ProductId.Value,
        lineItem.ProductName.Value,
        lineItem.Quantity.Value,
        lineItem.UnitPrice.Amount,
        lineItem.UnitPrice.Currency.Value);
}

public record CreateDraftOrderRequest
{
    public CustomerId CustomerId { get; init; } = null!;
    public List<LineItemInputRequest> LineItems { get; init; } = [];
}

public record LineItemInputRequest
{
    public ProductId ProductId { get; init; } = null!;
    public int Quantity { get; init; }
}

public record AddLineItemRequest
{
    public ProductId ProductId { get; init; } = null!;
    public int Quantity { get; init; }
}
