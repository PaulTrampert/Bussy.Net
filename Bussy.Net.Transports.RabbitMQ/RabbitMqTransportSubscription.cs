using Bussy.Net.Transport;

namespace Bussy.Net.Transports.RabbitMQ;

public sealed class RabbitMqTransportSubscription(string name, Func<ValueTask>? onDispose = null) : ITransportSubscription
{
    private readonly Func<ValueTask>? _onDispose = onDispose;
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

        if (_onDispose is not null)
        {
            await _onDispose();
        }
    }
}

