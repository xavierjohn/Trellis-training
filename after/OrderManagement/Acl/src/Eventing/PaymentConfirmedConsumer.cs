namespace OrderManagement.AntiCorruptionLayer.Eventing;

using System.Text.Json;
using Microsoft.Extensions.Hosting;
using OrderManagement.Application.IntegrationEvents;
using Trellis.Mediator;

/// <summary>
/// Background service that consumes <see cref="PaymentConfirmedIntegrationEvent"/> messages from
/// the in-memory broker and dispatches them through the inbox for idempotent processing.
/// </summary>
internal sealed class PaymentConfirmedConsumer(InMemoryEventBus bus, IInboxDispatcher inbox) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in bus.SubscribeAsync(PaymentConfirmedIntegrationEvent.MessageType, stoppingToken).ConfigureAwait(false))
        {
            var envelope = JsonSerializer.Deserialize<BrokerEnvelope<PaymentConfirmedIntegrationEvent>>(message, IntegrationEventSerialization.Options)
                ?? throw new JsonException("A payment message must contain a broker envelope.");
            await inbox.DispatchAsync(envelope.ToInboxEnvelope(), stoppingToken).ConfigureAwait(false);
        }
    }
}
