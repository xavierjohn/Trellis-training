#pragma warning disable CS1591
namespace OrderManagement.Api.v2026_11_12.Models;

using OrderManagement.Domain;
using Trellis.Primitives;

public record CreateDraftOrderRequest
{
    public CustomerId CustomerId { get; init; } = null!;
    public List<CreateDraftOrderLineItemRequest> LineItems { get; init; } = [];
}

public record CreateDraftOrderLineItemRequest
{
    public ProductId ProductId { get; init; } = null!;
    public LineItemQuantity Quantity { get; init; } = null!;
}

public record AddLineItemRequest
{
    public ProductId ProductId { get; init; } = null!;
    public LineItemQuantity Quantity { get; init; } = null!;
}

public record OrderResponse
{
    public Guid Id { get; init; }
    public Guid CustomerId { get; init; }
    public string CreatedByActorId { get; init; } = null!;
    public string Status { get; init; } = null!;
    public Money Total { get; init; } = null!;
    public DateTime CreatedAt { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public DateTime? ShippedAt { get; init; }
    public List<LineItemResponse> LineItems { get; init; } = [];

    public static OrderResponse From(Order order) => new()
    {
        Id = order.Id.Value,
        CustomerId = order.CustomerId.Value,
        CreatedByActorId = order.CreatedByActorId,
        Status = order.Status.ToString(),
        Total = order.CalculateTotal(),
        CreatedAt = order.CreatedAt,
        SubmittedAt = order.SubmittedAt.Match<DateTime?>(d => d, () => null),
        ShippedAt = order.ShippedAt.Match<DateTime?>(d => d, () => null),
        LineItems = order.LineItems.Select(LineItemResponse.From).ToList()
    };
}

public record LineItemResponse
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = null!;
    public int Quantity { get; init; }
    public Money UnitPrice { get; init; } = null!;

    public static LineItemResponse From(LineItem li) => new()
    {
        Id = li.Id.Value,
        ProductId = li.ProductId.Value,
        ProductName = li.ProductName.Value,
        Quantity = li.Quantity.Value,
        UnitPrice = li.UnitPrice
    };
}
