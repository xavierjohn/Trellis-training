namespace OrderManagement.Domain.Events;

using Trellis.Primitives;

public sealed record OrderSubmittedEvent(
    OrderId OrderId,
    CustomerId CustomerId,
    Money OrderTotal,
    DateTime SubmittedAt) : IDomainEvent
{
    public DateTime OccurredAt { get; } = SubmittedAt;
}

public sealed record OrderApprovedEvent(
    OrderId OrderId,
    DateTime ApprovedAt) : IDomainEvent
{
    public DateTime OccurredAt { get; } = ApprovedAt;
}

public sealed record OrderShippedEvent(
    OrderId OrderId,
    CustomerId CustomerId,
    DateTime ShippedAt) : IDomainEvent
{
    public DateTime OccurredAt { get; } = ShippedAt;
}

public sealed record OrderDeliveredEvent(
    OrderId OrderId,
    DateTime DeliveredAt) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DeliveredAt;
}

public sealed record OrderCancelledEvent(
    OrderId OrderId,
    OrderStatus CancelledFromStatus,
    DateTime CancelledAt) : IDomainEvent
{
    public DateTime OccurredAt { get; } = CancelledAt;
}

public sealed record OrderReturnedEvent(
    OrderId OrderId,
    CustomerId CustomerId,
    ReturnReason Reason,
    DateTime ReturnedAt) : IDomainEvent
{
    public DateTime OccurredAt { get; } = ReturnedAt;
}
