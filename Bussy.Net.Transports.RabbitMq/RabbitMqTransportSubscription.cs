using Bussy.Net.Transport;
using RabbitMQ.Client;

namespace Bussy.Net.Transports.RabbitMq;

/// <summary>
/// Represents an active RabbitMQ consumer subscription.
/// Disposing this object cancels message delivery and closes the underlying channel.
/// </summary>
public sealed class RabbitMqTransportSubscription(
    string name,
    string consumerTag,
    IChannel channel,
    CancellationTokenSource lifetimeCancellation) : ITransportSubscription
{
    private int _disposed;

    /// <inheritdoc/>
    public string Name { get; } = name;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _ = DisposeCoreAsync();
    }

    /// <inheritdoc/>
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
