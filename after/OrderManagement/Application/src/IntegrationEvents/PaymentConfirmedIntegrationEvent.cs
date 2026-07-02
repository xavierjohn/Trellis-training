namespace OrderManagement.Application.IntegrationEvents;

using Trellis;

/// <summary>
/// Contract for a payment confirmation notification received from the payments bounded context.
/// </summary>
public sealed record PaymentConfirmedIntegrationEvent(
    Guid EventId,
    Guid OrderId,
    decimal AmountPaid,
    string PaymentReference,
    DateTimeOffset OccurredAt,
    string Currency = "USD") : IIntegrationEvent
{
    /// <summary>The broker message type used to route this event.</summary>
    public const string MessageType = "payments.payment-confirmed.v1";
}
