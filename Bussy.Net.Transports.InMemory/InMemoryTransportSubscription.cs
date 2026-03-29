using Bussy.Net.Transport;

namespace Bussy.Net.Transports.InMemory;

public class InMemoryTransportSubscription : ITransportSubscription
{
    public string Name { get; init; }
    
    public void Dispose()
    {
        // TODO release managed resources here
    }

    public async ValueTask DisposeAsync()
    {
        // TODO release managed resources here
    }
}