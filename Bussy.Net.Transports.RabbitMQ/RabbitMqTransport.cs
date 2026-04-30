using Bussy.Net.Transport;

namespace Bussy.Net.Transports.RabbitMQ;

public sealed class RabbitMqTransport(RabbitMqTransportOptions options) : ITransport
{
    private readonly RabbitMqTransportOptions _options = options;

    public string Name => _options.TransportName;

    public TransportCapability Capabilities => TransportCapability.None;

    public Task SendAsync(OutboundMessage message, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("RabbitMQ send is not implemented yet.");
    }

    public Task SendBatchAsync(IReadOnlyCollection<OutboundMessage> messages, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("RabbitMQ batch send is not implemented yet.");
    }

    public Task<ITransportSubscription> SubscribeAsync(string topic, IInboundMessageHandler handler,
        CancellationToken cancellationToken = default)
    {
        ITransportSubscription subscription = new RabbitMqTransportSubscription($"{Name}_{topic}");
        return Task.FromResult(subscription);
    }
}
