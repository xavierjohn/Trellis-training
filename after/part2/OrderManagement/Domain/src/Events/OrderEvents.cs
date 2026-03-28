namespace OrderManagement.Domain;

using Trellis.Primitives;

/// <summary>Raised when an order is submitted by the customer.</summary>
public sealed record OrderSubmittedEvent(
    OrderId OrderId,
    CustomerId CustomerId,
    Money OrderTotal,
    DateTime OccurredAt) : IDomainEvent;

/// <summary>Raised when an order is approved by a warehouse manager.</summary>
public sealed record OrderApprovedEvent(
    OrderId OrderId,
    DateTime OccurredAt) : IDomainEvent;

/// <summary>Raised when an order is shipped.</summary>
public sealed record OrderShippedEvent(
    OrderId OrderId,
    CustomerId CustomerId,
    DateTime OccurredAt) : IDomainEvent;

/// <summary>Raised when an order is marked as delivered.</summary>
public sealed record OrderDeliveredEvent(
    OrderId OrderId,
    DateTime OccurredAt) : IDomainEvent;

/// <summary>Raised when an order is cancelled.</summary>
public sealed record OrderCancelledEvent(
    OrderId OrderId,
    OrderStatus CancelledFromStatus,
    DateTime OccurredAt) : IDomainEvent;
