namespace OrderManagement.Application.IntegrationEvents;

using Trellis;

/// <summary>
/// Stable, versioned contract published when an order is submitted.
/// </summary>
public sealed record OrderSubmittedIntegrationEvent(
    Guid EventId,
    Guid OrderId,
    Guid CustomerId,
    decimal OrderTotal,
    DateTimeOffset OccurredAt,
    string Currency = "USD") : IIntegrationEvent
{
    /// <summary>The broker message type used to route this event.</summary>
    public const string MessageType = "orders.order-submitted.v1";
}
