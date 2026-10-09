namespace OrderManagement.AntiCorruptionLayer.Eventing;

using System.Text.Json;
using Microsoft.Extensions.Hosting;
using OrderManagement.Application.IntegrationEvents;
using Trellis.Mediator;

/// <summary>
/// Development-only background service that simulates an external payments service: whenever an
/// order is submitted, it "confirms payment" shortly after by publishing a
/// <see cref="PaymentConfirmedIntegrationEvent"/> back onto the broker.
/// </summary>
internal sealed class PaymentSimulator(InMemoryEventBus bus, TimeProvider timeProvider) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in bus.SubscribeAsync(OrderSubmittedIntegrationEvent.MessageType, stoppingToken).ConfigureAwait(false))
        {
            var submitted = JsonSerializer.Deserialize<BrokerEnvelope<OrderSubmittedIntegrationEvent>>(message, IntegrationEventSerialization.Options)
                ?? throw new JsonException("An order submission must contain a broker envelope.");
            var evt = submitted.Event;

            var confirmedAt = timeProvider.GetUtcNow();
            var paymentEvent = new PaymentConfirmedIntegrationEvent(
                DeterministicEventId.ForOrder(evt.OrderId, "payment"),
                evt.OrderId,
                evt.OrderTotal,
                $"PAY-{evt.OrderId:N}",
                confirmedAt,
                evt.Currency);

            var outbound = new OutboundIntegrationMessage(paymentEvent.EventId, paymentEvent)
            {
                MessageSource = "payments",
                CausationId = submitted.MessageId,
                CorrelationId = submitted.CorrelationId,
                TraceParent = submitted.TraceParent,
                TraceState = submitted.TraceState,
            };
            var bytes = JsonSerializer.SerializeToUtf8Bytes(
                BrokerEnvelope<PaymentConfirmedIntegrationEvent>.From(outbound, paymentEvent), IntegrationEventSerialization.Options);
            await bus.PublishAsync(PaymentConfirmedIntegrationEvent.MessageType, bytes, stoppingToken).ConfigureAwait(false);
        }
    }
}
