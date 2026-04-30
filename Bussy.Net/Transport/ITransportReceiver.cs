using System.Threading;
using System.Threading.Tasks;

namespace Bussy.Net.Transport;

/// <summary>
/// Receives messages from a backing broker implementation.
/// </summary>
public interface ITransportReceiver
{
    /// <summary>
    /// Starts a subscription and invokes the message handler for each delivered message.
    /// </summary>
    Task<ITransportSubscription> SubscribeAsync(
        string topic,
        IInboundMessageHandler handler,
        CancellationToken cancellationToken = default);
}
