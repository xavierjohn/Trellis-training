namespace OrderManagement.Api.v2026_11_12.Models;

using OrderManagement.Application.Orders;
using OrderManagement.Domain;

/// <summary>Line-item response nested inside an order response.</summary>
public record LineItemResponse
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = null!;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal LineTotal { get; init; }

    public static LineItemResponse From(LineItem li) => new()
    {
        Id = li.Id,
        ProductId = li.ProductId,
        ProductName = li.ProductName,
        Quantity = li.Quantity,
        UnitPrice = li.UnitPrice,
        LineTotal = li.LineTotal,
    };
}

/// <summary>Order response model.</summary>
public record OrderResponse
{
    public Guid Id { get; init; }
    public Guid CustomerId { get; init; }
    public string CreatedByActorId { get; init; } = null!;
    public string Status { get; init; } = null!;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? SubmittedAt { get; init; }
    public DateTimeOffset? ShippedAt { get; init; }
    public DateTimeOffset? PaidAt { get; init; }
    public string? PaymentReference { get; init; }
    public decimal? PaidAmount { get; init; }
    public IReadOnlyList<LineItemResponse> LineItems { get; init; } = [];
    public decimal OrderTotal { get; init; }

    public static OrderResponse From(Order order) => new()
    {
        Id = order.Id,
        CustomerId = order.CustomerId,
        CreatedByActorId = order.CreatedByActorId,
        Status = order.Status,
        CreatedAt = order.CreatedAt,
        SubmittedAt = order.SubmittedAt.Match<DateTimeOffset?>(t => t, () => null),
        ShippedAt = order.ShippedAt.Match<DateTimeOffset?>(t => t, () => null),
        PaidAt = order.PaidAt.Match<DateTimeOffset?>(t => t, () => null),
        PaymentReference = order.PaymentReference.Match<string?>(r => r.Value, () => null),
        PaidAmount = order.PaidAmount.Match<decimal?>(a => a, () => null),
        LineItems = order.LineItems.Select(LineItemResponse.From).ToList(),
        OrderTotal = order.OrderTotal,
    };
}

/// <summary>Line-item shape inside <see cref="CreateOrderRequest"/>.</summary>
public record CreateOrderLineRequest
{
    public ProductId ProductId { get; init; } = null!;
    public LineItemQuantity Quantity { get; init; } = null!;

    public DraftLineItem ToDomain() => new(ProductId, Quantity);
}

/// <summary>Request model for creating a draft order.</summary>
public record CreateOrderRequest
{
    public CustomerId CustomerId { get; init; } = null!;
    public IReadOnlyList<CreateOrderLineRequest> LineItems { get; init; } = [];
}

/// <summary>Request model for adding a line item to a draft order.</summary>
public record AddLineItemRequest
{
    public ProductId ProductId { get; init; } = null!;
    public LineItemQuantity Quantity { get; init; } = null!;
}
