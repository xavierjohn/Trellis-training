namespace OrderManagement.Api.v2026_11_12.Models;

using OrderManagement.Domain;

/// <summary>
/// Response model for an order.
/// </summary>
public record OrderResponse
{
    /// <summary>Order identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Customer identifier associated with the order.</summary>
    public Guid CustomerId { get; init; }

    /// <summary>Actor identifier of the user who created the order.</summary>
    public string CreatedByActorId { get; init; } = null!;

    /// <summary>Current status of the order.</summary>
    public string Status { get; init; } = null!;

    /// <summary>Total monetary value of the order.</summary>
    public MoneyResponse Total { get; init; } = null!;

    /// <summary>Date and time the order was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Date and time the order was submitted, if applicable.</summary>
    public DateTime? SubmittedAt { get; init; }

    /// <summary>Date and time the order was shipped, if applicable.</summary>
    public DateTime? ShippedAt { get; init; }

    /// <summary>Line items included in the order.</summary>
    public List<LineItemResponse> LineItems { get; init; } = [];

    /// <summary>Maps from domain model to response.</summary>
    public static OrderResponse From(Order order) => new()
    {
        Id = order.Id.Value,
        CustomerId = order.CustomerId.Value,
        CreatedByActorId = order.CreatedByActorId,
        Status = order.Status.ToString(),
        Total = new MoneyResponse { Amount = order.Total.Amount, Currency = order.Total.Currency.Value },
        CreatedAt = order.CreatedAt,
        SubmittedAt = order.SubmittedAt.Match<DateTime?>(d => d, () => null),
        ShippedAt = order.ShippedAt.Match<DateTime?>(d => d, () => null),
        LineItems = order.LineItems.Select(li => new LineItemResponse
        {
            Id = li.Id.Value,
            ProductId = li.ProductId.Value,
            ProductName = li.ProductName.Value,
            Quantity = li.Quantity.Value,
            UnitPrice = new MoneyResponse { Amount = li.UnitPrice.Amount, Currency = li.UnitPrice.Currency.Value }
        }).ToList()
    };
}

/// <summary>
/// Response model for an order line item.
/// </summary>
public record LineItemResponse
{
    /// <summary>Line item identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Product identifier for the line item.</summary>
    public Guid ProductId { get; init; }

    /// <summary>Name of the product.</summary>
    public string ProductName { get; init; } = null!;

    /// <summary>Quantity ordered.</summary>
    public int Quantity { get; init; }

    /// <summary>Unit price of the product.</summary>
    public MoneyResponse UnitPrice { get; init; } = null!;
}
