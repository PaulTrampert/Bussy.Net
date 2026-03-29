using System.Collections.Concurrent;
using System.Threading.Channels;
using Bussy.Net.Transport;
using Microsoft.Extensions.Logging;

namespace Bussy.Net.Transports.InMemory;

public class InMemoryTransport(ILoggerFactory loggerFactory) : ITransport
{
    private readonly ConcurrentDictionary<string, InMemoryTransportSubscription> _subscriptions = new();
    
    public string Name => "InMemory";
    public TransportCapability Capabilities => TransportCapability.None;

    public async Task SendAsync(OutboundMessage message, CancellationToken cancellationToken = default)
    {
        if (!_subscriptions.TryGetValue(message.Topic, out var subscription))
        {
            return;
        }
        var inboundMessage = new InboundMessage(
            message.Body,
            message.Topic,
            message.Broker,
            message.Headers,
            message.MessageId,
            message.SentAtUtc,
            DateTimeOffset.UtcNow,
            0);
        
        await subscription.EnqueueAsync(inboundMessage, cancellationToken);
    }

    public async Task SendBatchAsync(IReadOnlyCollection<OutboundMessage> messages, CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(messages.Select(m => SendAsync(m, cancellationToken)));
    }
    
    public Task<ITransportSubscription> SubscribeAsync(string topic, Func<InboundMessage, CancellationToken, Task<AckAction>> onMessage, CancellationToken cancellationToken = default)
    {
        var subscription = _subscriptions.GetOrAdd(topic, key => new InMemoryTransportSubscription($"{Name}_{key}", onMessage, loggerFactory.CreateLogger<InMemoryTransportSubscription>(), cancellationToken));
        return Task.FromResult<ITransportSubscription>(subscription);
    }
}