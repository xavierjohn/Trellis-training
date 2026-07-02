namespace OrderManagement.AntiCorruptionLayer.Eventing;

using System.Text.Json;

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
