namespace OrderManagement.AntiCorruptionLayer.Eventing;

using System.Collections.Concurrent;
using System.Threading.Channels;

/// <summary>
/// A minimal in-process message bus that simulates a message broker for demo/testing purposes.
/// Messages are routed by a string message type to an unbounded channel per type.
/// </summary>
internal sealed class InMemoryEventBus
{
    private readonly ConcurrentDictionary<string, Channel<byte[]>> _channels = new();

    /// <summary>Publishes a serialized message payload under the given message type.</summary>
    public ValueTask PublishAsync(string messageType, byte[] payload, CancellationToken cancellationToken)
    {
        var channel = _channels.GetOrAdd(messageType, static _ => Channel.CreateUnbounded<byte[]>());
        return channel.Writer.WriteAsync(payload, cancellationToken);
    }

    /// <summary>Subscribes to all messages published under the given message type.</summary>
    public IAsyncEnumerable<byte[]> SubscribeAsync(string messageType, CancellationToken cancellationToken)
    {
        var channel = _channels.GetOrAdd(messageType, static _ => Channel.CreateUnbounded<byte[]>());
        return channel.Reader.ReadAllAsync(cancellationToken);
    }
}
