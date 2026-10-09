namespace OrderManagement.AntiCorruptionLayer.Eventing;

using System.Text.Json;
using Trellis.Mediator;

/// <summary>
/// Shared JSON serializer options for integration-event payloads exchanged over the in-memory
/// broker. Serialization is a transport concern owned by the anti-corruption layer, so these
/// options live here rather than in the Application layer alongside the event contracts.
/// </summary>
internal static class IntegrationEventSerialization
{
    /// <summary>Web-defaults serializer options (camelCase) used to serialize/deserialize integration events.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

internal sealed record BrokerEnvelope<TEvent>(Guid MessageId, TEvent Event)
    where TEvent : IIntegrationEvent
{
    public string? MessageSource { get; init; }
    public Guid? CausationId { get; init; }
    public string? CorrelationId { get; init; }
    public string? TraceParent { get; init; }
    public string? TraceState { get; init; }

    public static BrokerEnvelope<TEvent> From(OutboundIntegrationMessage message, TEvent evt) =>
        new(message.MessageId, evt)
        {
            MessageSource = message.MessageSource,
            CausationId = message.CausationId,
            CorrelationId = message.CorrelationId,
            TraceParent = message.TraceParent,
            TraceState = message.TraceState,
        };

    public IntegrationEnvelope ToInboxEnvelope() =>
        new(MessageId, Event)
        {
            MessageSource = MessageSource,
            CausationId = CausationId,
            CorrelationId = CorrelationId,
            TraceParent = TraceParent,
            TraceState = TraceState,
        };
}
