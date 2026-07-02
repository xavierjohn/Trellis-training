namespace OrderManagement.Application.Orders;

using OrderManagement.Application.IntegrationEvents;
using OrderManagement.Domain;
using Trellis.Mediator;

/// <summary>
/// Translates the internal <see cref="OrderSubmittedEvent"/> into the stable
/// <see cref="OrderSubmittedIntegrationEvent"/> contract. The collected integration event is
/// persisted to the outbox in the same transaction as the order change and relayed after commit.
/// </summary>
internal sealed class OrderSubmittedTranslator(IIntegrationEventCollector collector) : IDomainEventHandler<OrderSubmittedEvent>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(OrderSubmittedEvent domainEvent, CancellationToken cancellationToken)
    {
        collector.Add(new OrderSubmittedIntegrationEvent(
            DeterministicEventId.ForOrder(domainEvent.OrderId, "submitted"),
            domainEvent.OrderId,
            domainEvent.CustomerId,
            domainEvent.OrderTotal,
            domainEvent.OccurredAt));
        return ValueTask.CompletedTask;
    }
}
