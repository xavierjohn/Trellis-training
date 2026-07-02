namespace OrderManagement.Application.IntegrationEvents;

using Trellis;

/// <summary>
/// Stable, versioned contract published when an order is cancelled.
/// </summary>
public sealed record OrderCancelledIntegrationEvent(
    Guid EventId,
    Guid OrderId,
    string CancelledFromStatus,
    DateTimeOffset OccurredAt) : IIntegrationEvent
{
    /// <summary>The broker message type used to route this event.</summary>
    public const string MessageType = "orders.order-cancelled.v1";
}
