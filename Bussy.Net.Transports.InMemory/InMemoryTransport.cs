using Bussy.Net.Transport;

namespace Bussy.Net.Transports.InMemory;

public class InMemoryTransport : ITransport
{
    public Task SendAsync(OutboundMessage message, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task SendBatchAsync(IReadOnlyCollection<OutboundMessage> messages, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<ITransportSubscription> SubscribeAsync(string topic, Func<InboundMessage, CancellationToken, Task<AckAction>> onMessage, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public string Name { get; }
    public TransportCapability Capabilities { get; }
}