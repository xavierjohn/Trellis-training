namespace OrderManagement.Domain;

/// <summary>
/// Domain events raised by the Order aggregate. Each event's <c>OccurredAt</c> field
/// is the canonical timestamp per <see cref="IDomainEvent"/> — the spec's
/// "SubmittedAt", "ApprovedAt", "ShippedAt", etc. names correspond to the same instant.
/// </summary>
public sealed record OrderSubmittedEvent(
    OrderId OrderId,
    CustomerId CustomerId,
    decimal OrderTotal,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OrderApprovedEvent(
    OrderId OrderId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OrderShippedEvent(
    OrderId OrderId,
    CustomerId CustomerId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OrderDeliveredEvent(
    OrderId OrderId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OrderCancelledEvent(
    OrderId OrderId,
    OrderStatus CancelledFromStatus,
    DateTimeOffset OccurredAt) : IDomainEvent;
