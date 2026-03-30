namespace OrderManagement.Domain;

using Trellis.Primitives;

public sealed record OrderSubmittedEvent(OrderId OrderId, CustomerId CustomerId, Money OrderTotal, DateTime OccurredAt) : IDomainEvent;

public sealed record OrderApprovedEvent(OrderId OrderId, DateTime OccurredAt) : IDomainEvent;

public sealed record OrderShippedEvent(OrderId OrderId, CustomerId CustomerId, DateTime OccurredAt) : IDomainEvent;

public sealed record OrderDeliveredEvent(OrderId OrderId, DateTime OccurredAt) : IDomainEvent;

public sealed record OrderCancelledEvent(OrderId OrderId, OrderStatus CancelledFromStatus, DateTime OccurredAt) : IDomainEvent;
