namespace AntiCorruptionLayer.Tests;

using System.Text.Json;
using OrderManagement.AntiCorruptionLayer.Eventing;
using OrderManagement.Application.IntegrationEvents;
using Trellis.Mediator;

public class BrokerIntegrationEventPublisherTests
{
    [Fact]
    public async Task PublishAsync_Redelivery_PreservesMessageIdentityAndLineage()
    {
        var ct = TestContext.Current.CancellationToken;
        var bus = new InMemoryEventBus();
        var publisher = new BrokerIntegrationEventPublisher(bus);
        var evt = new OrderSubmittedIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 12m, DateTimeOffset.UtcNow);
        var causationId = Guid.NewGuid();
        var message = new OutboundIntegrationMessage(Guid.NewGuid(), evt)
        {
            MessageSource = "orders",
            CausationId = causationId,
            CorrelationId = "order-workflow",
            TraceParent = "00-0123456789abcdef0123456789abcdef-0123456789abcdef-01",
            TraceState = "demo=one",
        };

        await publisher.PublishAsync(message, ct);
        await publisher.PublishAsync(message, ct);
        await using var deliveries = bus.SubscribeAsync(OrderSubmittedIntegrationEvent.MessageType, ct).GetAsyncEnumerator(ct);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            (await deliveries.MoveNextAsync()).Should().BeTrue();
            using var json = JsonDocument.Parse(deliveries.Current);
            var wire = json.RootElement;
            wire.GetProperty("messageId").GetGuid().Should().Be(message.MessageId);
            wire.GetProperty("messageSource").GetString().Should().Be(message.MessageSource);
            wire.GetProperty("causationId").GetGuid().Should().Be(causationId);
            wire.GetProperty("correlationId").GetString().Should().Be(message.CorrelationId);
            wire.GetProperty("traceParent").GetString().Should().Be(message.TraceParent);
            wire.GetProperty("traceState").GetString().Should().Be(message.TraceState);
            wire.GetProperty("event").GetProperty("eventId").GetGuid().Should().Be(evt.EventId);
        }
    }
}
