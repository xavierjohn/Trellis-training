namespace AntiCorruptionLayer.Tests;

using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using OrderManagement.AntiCorruptionLayer.Eventing;
using OrderManagement.Application.IntegrationEvents;
using Trellis.Mediator;

public class BrokerRoundTripTests
{
    [Fact]
    public async Task ConsumeAsync_PaymentEnvelope_PreservesTransportIdentityAndLineage()
    {
        var ct = TestContext.Current.CancellationToken;
        var bus = new InMemoryEventBus();
        var inbox = new CapturingInbox();
        using var consumer = new PaymentConfirmedConsumer(bus, inbox);
        await consumer.StartAsync(ct);
        try
        {
            var evt = new PaymentConfirmedIntegrationEvent(
                Guid.NewGuid(), Guid.NewGuid(), 12m, "PAY-test", DateTimeOffset.UtcNow);
            var message = new OutboundIntegrationMessage(Guid.NewGuid(), evt)
            {
                MessageSource = "payments",
                CausationId = Guid.NewGuid(),
                CorrelationId = "workflow",
                TraceParent = "00-0123456789abcdef0123456789abcdef-0123456789abcdef-01",
                TraceState = "demo=one",
            };

            var bytes = JsonSerializer.SerializeToUtf8Bytes(
                BrokerEnvelope<PaymentConfirmedIntegrationEvent>.From(message, evt), IntegrationEventSerialization.Options);
            await bus.PublishAsync(PaymentConfirmedIntegrationEvent.MessageType, bytes, ct);
            var received = await inbox.Received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

            received.MessageId.Should().Be(message.MessageId).And.NotBe(evt.EventId);
            received.Event.Should().BeEquivalentTo(evt);
            received.MessageSource.Should().Be(message.MessageSource);
            received.CausationId.Should().Be(message.CausationId);
            received.CorrelationId.Should().Be(message.CorrelationId);
            received.TraceParent.Should().Be(message.TraceParent);
            received.TraceState.Should().Be(message.TraceState);
        }
        finally { await consumer.StopAsync(ct); }
    }

    [Fact]
    public async Task PublishAsync_SubmissionSimulatorConsumer_PreservesWorkflowLineage()
    {
        var ct = TestContext.Current.CancellationToken;
        var bus = new InMemoryEventBus();
        var inbox = new CapturingInbox();
        var time = new FakeTimeProvider();
        using var simulator = new PaymentSimulator(bus, time);
        using var consumer = new PaymentConfirmedConsumer(bus, inbox);
        await simulator.StartAsync(ct);
        await consumer.StartAsync(ct);
        try
        {
            var evt = new OrderSubmittedIntegrationEvent(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 12m, time.GetUtcNow());
            var message = new OutboundIntegrationMessage(Guid.NewGuid(), evt)
            {
                CorrelationId = "workflow",
                TraceParent = "00-0123456789abcdef0123456789abcdef-0123456789abcdef-01",
                TraceState = "demo=one",
            };

            await new BrokerIntegrationEventPublisher(bus).PublishAsync(message, ct);
            var received = await inbox.Received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            var payment = received.Event.Should().BeOfType<PaymentConfirmedIntegrationEvent>().Which;

            payment.OrderId.Should().Be(evt.OrderId);
            payment.AmountPaid.Should().Be(evt.OrderTotal);
            payment.OccurredAt.Should().Be(time.GetUtcNow());
            received.MessageId.Should().Be(payment.EventId);
            received.MessageSource.Should().Be("payments");
            received.CausationId.Should().Be(message.MessageId).And.NotBe(evt.EventId);
            received.CorrelationId.Should().Be(message.CorrelationId);
            received.TraceParent.Should().Be(message.TraceParent);
            received.TraceState.Should().Be(message.TraceState);
        }
        finally
        {
            await simulator.StopAsync(ct);
            await consumer.StopAsync(ct);
        }
    }

    private sealed class CapturingInbox : IInboxDispatcher
    {
        public TaskCompletionSource<IntegrationEnvelope> Received { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<InboxDispatchOutcome> DispatchAsync(IntegrationEnvelope envelope, CancellationToken cancellationToken = default)
        {
            Received.TrySetResult(envelope);
            return Task.FromResult(InboxDispatchOutcome.Processed);
        }
    }
}
