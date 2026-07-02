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
        await foreach (var message in bus.SubscribeAsync(PaymentConfirmedIntegrationEvent.MessageType, stoppingToken))
        {
            var evt = JsonSerializer.Deserialize<PaymentConfirmedIntegrationEvent>(message, IntegrationEventSerialization.Options);
            if (evt is null)
                continue;

            var envelope = new IntegrationEnvelope(evt.EventId, evt) { MessageSource = "payments" };
            await inbox.DispatchAsync(envelope, stoppingToken);
        }
    }
}
