namespace OrderManagement.Application.IntegrationEvents;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Generates deterministic UUIDv5 event ids so a retried domain-event translation produces the
/// same integration event id for the same business fact, aiding consumer-side de-duplication.
/// </summary>
public static class DeterministicEventId
{
    private static readonly Guid Namespace = new("b0e1a2c3-d4e5-f6a7-b8c9-d0e1f2a3b4c5");

    /// <summary>
    /// Computes a deterministic event id for an order-scoped fact, discriminated by
    /// <paramref name="discriminator"/> (e.g. <c>"submitted"</c>, <c>"cancelled"</c>).
    /// </summary>
    public static Guid ForOrder(Guid orderId, string discriminator)
    {
        var input = $"{orderId:N}:{discriminator}";
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var namespaceBytes = Namespace.ToByteArray();

        // Swap to big-endian for UUID v5 namespace per RFC 4122.
        Array.Reverse(namespaceBytes, 0, 4);
        Array.Reverse(namespaceBytes, 4, 2);
        Array.Reverse(namespaceBytes, 6, 2);

#pragma warning disable CA5350 // SHA-1 used only to derive a deterministic UUIDv5, not for cryptographic security.
        var hash = SHA1.HashData([.. namespaceBytes, .. inputBytes]);
#pragma warning restore CA5350
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50); // Version 5
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80); // Variant

        // Convert back to little-endian Guid layout.
        Array.Reverse(hash, 0, 4);
        Array.Reverse(hash, 4, 2);
        Array.Reverse(hash, 6, 2);
        return new Guid(hash[..16]);
    }
}
