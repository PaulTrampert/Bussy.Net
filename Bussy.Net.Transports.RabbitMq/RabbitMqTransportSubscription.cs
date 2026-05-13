using Bussy.Net.Transport;
using RabbitMQ.Client;

namespace Bussy.Net.Transports.RabbitMq;

public sealed class RabbitMqTransportSubscription(
    string name,
    string consumerTag,
    IChannel channel,
    CancellationTokenSource lifetimeCancellation) : ITransportSubscription
{
    private int _disposed;

    public string Name { get; } = name;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _ = DisposeCoreAsync();
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return ValueTask.CompletedTask;
        }

        return new ValueTask(DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        lifetimeCancellation.Cancel();
        await channel.BasicCancelAsync(consumerTag).ConfigureAwait(false);
        await channel.DisposeAsync().ConfigureAwait(false);
        lifetimeCancellation.Dispose();
    }
}
