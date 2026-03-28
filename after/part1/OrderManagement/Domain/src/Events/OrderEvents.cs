namespace OrderManagement.Domain;

using Trellis.Primitives;

/// <summary>Raised when an order is submitted.</summary>
public sealed record OrderSubmittedEvent(OrderId OrderId, CustomerId CustomerId, Money OrderTotal, DateTime SubmittedAt) : IDomainEvent
{
    public DateTime OccurredAt => SubmittedAt;
}

/// <summary>Raised when an order is approved.</summary>
public sealed record OrderApprovedEvent(OrderId OrderId, DateTime ApprovedAt) : IDomainEvent
{
    public DateTime OccurredAt => ApprovedAt;
}

/// <summary>Raised when an order is shipped.</summary>
public sealed record OrderShippedEvent(OrderId OrderId, CustomerId CustomerId, DateTime ShippedAt) : IDomainEvent
{
    public DateTime OccurredAt => ShippedAt;
}

/// <summary>Raised when an order is delivered.</summary>
public sealed record OrderDeliveredEvent(OrderId OrderId, DateTime DeliveredAt) : IDomainEvent
{
    public DateTime OccurredAt => DeliveredAt;
}

/// <summary>Raised when an order is cancelled.</summary>
public sealed record OrderCancelledEvent(OrderId OrderId, OrderStatus CancelledFromStatus, DateTime CancelledAt) : IDomainEvent
{
    public DateTime OccurredAt => CancelledAt;
}
