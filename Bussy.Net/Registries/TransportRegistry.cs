using System.Collections.Concurrent;
using Bussy.Net.Transport;

namespace Bussy.Net.Registries;

internal class TransportRegistry
{
    public ConcurrentDictionary<string, ITransport> Transports { get; } = new();

    public void Register(ITransport transport)
    {
        Transports.TryAdd(transport.Name, transport);
    }
}