namespace OrderManagement.Application.Orders;

using OrderManagement.Application.IntegrationEvents;
using OrderManagement.Domain;
using Trellis.Mediator;

/// <summary>
/// Translates the internal <see cref="OrderCancelledEvent"/> into the stable
/// <see cref="OrderCancelledIntegrationEvent"/> contract. The collected integration event is
/// enrolled by the relay after the order commits, atomically with the relay's handler progress.
/// </summary>
internal sealed class OrderCancelledTranslator(IIntegrationEventCollector collector) : IDomainEventHandler<OrderCancelledEvent>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(OrderCancelledEvent domainEvent, CancellationToken cancellationToken)
    {
        collector.Add(new OrderCancelledIntegrationEvent(
            DeterministicEventId.ForOrder(domainEvent.OrderId, "cancelled"),
            domainEvent.OrderId,
            domainEvent.CancelledFromStatus,
            domainEvent.OccurredAt));
        return ValueTask.CompletedTask;
    }
}
