namespace OrderManagement.Domain.Events;

using OrderManagement.Domain.ValueObjects;
using Trellis.Primitives;

public sealed record OrderSubmittedEvent(
    OrderId OrderId,
    CustomerId CustomerId,
    Money OrderTotal,
    DateTime SubmittedAt) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

public sealed record OrderApprovedEvent(
    OrderId OrderId,
    DateTime ApprovedAt) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

public sealed record OrderShippedEvent(
    OrderId OrderId,
    CustomerId CustomerId,
    DateTime ShippedAt) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

public sealed record OrderDeliveredEvent(
    OrderId OrderId,
    DateTime DeliveredAt) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

public sealed record OrderCancelledEvent(
    OrderId OrderId,
    OrderStatus CancelledFromStatus,
    DateTime CancelledAt) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
