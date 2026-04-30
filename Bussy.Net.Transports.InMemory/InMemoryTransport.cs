using System.Collections.Concurrent;
using System.Threading.Channels;
using Bussy.Net.Transport;
using Microsoft.Extensions.Logging;

namespace Bussy.Net.Transports.InMemory;

public class InMemoryTransport(ILoggerFactory loggerFactory) : ITransport
{
    private readonly ConcurrentDictionary<string, IEnumerable<InMemoryTransportSubscription>> _subscriptions = new();
    
    public string Name => "InMemory";
    public TransportCapability Capabilities => TransportCapability.None;

    public async Task SendAsync(OutboundMessage message, CancellationToken cancellationToken = default)
    {
        if (!_subscriptions.TryGetValue(message.Topic, out var subscriptions))
        {
            return;
        }

        await Task.WhenAll(subscriptions.Select(async s =>
        {
            var inboundMessage = new InboundMessage(
                message.Body,
                message.Topic,
                message.Broker,
                message.Headers,
                message.MessageId,
                message.SentAtUtc,
                DateTimeOffset.UtcNow,
                0);
            await s.EnqueueAsync(inboundMessage, cancellationToken);
        }));
    }

    public async Task SendBatchAsync(IReadOnlyCollection<OutboundMessage> messages, CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(messages.Select(m => SendAsync(m, cancellationToken)));
    }
    
    public Task<ITransportSubscription> SubscribeAsync(string topic, IInboundMessageHandler handler, CancellationToken cancellationToken = default)
    {
        var subscription = new InMemoryTransportSubscription($"{Name}_{topic}", handler, loggerFactory.CreateLogger<InMemoryTransportSubscription>(), cancellationToken);
        _subscriptions.AddOrUpdate(topic, _ => [subscription], (_, old) => old.Append(subscription));
        return Task.FromResult<ITransportSubscription>(subscription);
    }
}