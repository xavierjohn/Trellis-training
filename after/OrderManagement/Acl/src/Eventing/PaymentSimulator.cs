namespace OrderManagement.AntiCorruptionLayer.Eventing;

using System.Text.Json;
using Microsoft.Extensions.Hosting;
using OrderManagement.Application.IntegrationEvents;

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
        await foreach (var message in bus.SubscribeAsync(OrderSubmittedIntegrationEvent.MessageType, stoppingToken))
        {
            var evt = JsonSerializer.Deserialize<OrderSubmittedIntegrationEvent>(message, IntegrationEventSerialization.Options);
            if (evt is null)
                continue;

            var confirmedAt = timeProvider.GetUtcNow();
            var paymentEvent = new PaymentConfirmedIntegrationEvent(
                DeterministicEventId.ForOrder(evt.OrderId, "payment"),
                evt.OrderId,
                evt.OrderTotal,
                $"PAY-{evt.OrderId:N}",
                confirmedAt,
                evt.Currency);

            var bytes = JsonSerializer.SerializeToUtf8Bytes(paymentEvent, IntegrationEventSerialization.Options);
            await bus.PublishAsync(PaymentConfirmedIntegrationEvent.MessageType, bytes, stoppingToken);
        }
    }
}
