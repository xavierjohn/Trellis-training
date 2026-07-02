namespace OrderManagement.AntiCorruptionLayer.Eventing;

using System.Text.Json;
using OrderManagement.Application.IntegrationEvents;
using Trellis;
using Trellis.Mediator;

/// <summary>
/// Publishes integration events to the in-memory broker instead of the default in-process fan-out,
/// so consumers (including out-of-process simulators) receive them via <see cref="InMemoryEventBus"/>.
/// </summary>
internal sealed class BrokerIntegrationEventPublisher(InMemoryEventBus bus) : IIntegrationEventPublisher
{
    /// <inheritdoc />
    public async ValueTask PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var (messageType, bytes) = integrationEvent switch
        {
            OrderSubmittedIntegrationEvent e => (
                OrderSubmittedIntegrationEvent.MessageType,
                JsonSerializer.SerializeToUtf8Bytes(e, IntegrationEventSerialization.Options)),
            OrderCancelledIntegrationEvent e => (
                OrderCancelledIntegrationEvent.MessageType,
                JsonSerializer.SerializeToUtf8Bytes(e, IntegrationEventSerialization.Options)),
            _ => throw new NotSupportedException($"No broker mapping for integration event type '{integrationEvent.GetType().Name}'."),
        };

        await bus.PublishAsync(messageType, bytes, cancellationToken);
    }
}
