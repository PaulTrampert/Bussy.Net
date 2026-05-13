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
    /// <param name="topic">The topic, queue, or route to subscribe to.</param>
    /// <param name="handler">The handler that will be invoked for each incoming message.</param>
    /// <param name="cancellationToken">Token to cancel the subscription setup.</param>
    /// <returns>
    /// A task that resolves to an <see cref="ITransportSubscription"/> representing the active subscription.
    /// Dispose the subscription to stop receiving messages.
    /// </returns>
    Task<ITransportSubscription> SubscribeAsync(
        string topic,
        IInboundMessageHandler handler,
        CancellationToken cancellationToken = default);
}
