namespace OrderManagement.Application.Orders;

using OrderManagement.Application.IntegrationEvents;
using OrderManagement.Domain;
using Trellis.Mediator;

/// <summary>
/// Translates the internal <see cref="OrderCancelledEvent"/> into the stable
/// <see cref="OrderCancelledIntegrationEvent"/> contract. The collected integration event is
/// persisted to the outbox in the same transaction as the order change and relayed after commit.
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
