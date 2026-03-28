namespace OrderManagement.Api.v2026_11_12.Models;

using OrderManagement.Domain;

/// <summary>
/// Response model for an order.
/// </summary>
public record OrderResponse
{
    /// <summary>Unique order identifier.</summary>
    public Guid Id { get; init; }
    /// <summary>Customer who placed the order.</summary>
    public Guid CustomerId { get; init; }
    /// <summary>Identity of the actor who created the order.</summary>
    public string CreatedByActorId { get; init; } = null!;
    /// <summary>Current lifecycle status.</summary>
    public string Status { get; init; } = null!;
    /// <summary>Sum of all line item prices.</summary>
    public MoneyDto Total { get; init; } = null!;
    /// <summary>UTC timestamp when the order was created.</summary>
    public DateTime CreatedAt { get; init; }
    /// <summary>UTC timestamp when the order was submitted, if applicable.</summary>
    public DateTime? SubmittedAt { get; init; }
    /// <summary>UTC timestamp when the order was shipped, if applicable.</summary>
    public DateTime? ShippedAt { get; init; }
    /// <summary>Line items in the order.</summary>
    public IReadOnlyList<LineItemResponse> LineItems { get; init; } = [];

    /// <summary>Maps from domain aggregate.</summary>
    public static OrderResponse From(Order o) => new()
    {
        Id = o.Id.Value,
        CustomerId = o.CustomerId.Value,
        CreatedByActorId = o.CreatedByActorId,
        Status = o.Status.ToString(),
        Total = MoneyDto.From(o.ComputeTotal()),
        CreatedAt = o.CreatedAt,
        SubmittedAt = o.SubmittedAt.Match<DateTime?>(v => v, () => null),
        ShippedAt = o.ShippedAt.Match<DateTime?>(v => v, () => null),
        LineItems = o.LineItems.Select(LineItemResponse.From).ToList()
    };
}
