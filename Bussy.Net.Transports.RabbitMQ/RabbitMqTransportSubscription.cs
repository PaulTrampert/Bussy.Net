using Bussy.Net.Transport;

namespace Bussy.Net.Transports.RabbitMQ;

public sealed class RabbitMqTransportSubscription(string name) : ITransportSubscription
{
    public string Name { get; } = name;

    public void Dispose()
    {
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

