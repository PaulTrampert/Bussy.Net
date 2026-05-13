using Bussy.Net.Transport;
using RabbitMQ.Client;

namespace Bussy.Net.Transports.RabbitMq;

public sealed class RabbitMqTransportSubscription(string name, string consumerTag, IChannel channel) : ITransportSubscription
{
    private int _disposed;

    public string Name { get; } = name;

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await channel.BasicCancelAsync(consumerTag);
        await channel.DisposeAsync();
    }
}

