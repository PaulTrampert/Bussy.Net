using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bussy.Net.Transport;

/// <summary>
/// Receives messages from a backing broker implementation.
/// </summary>
public interface ITransportReceiver
{
    /// <summary>
    /// Starts a subscription and invokes the callback for each delivered message.
    /// </summary>
    Task<ITransportSubscription> SubscribeAsync(
        SubscriptionDefinition definition,
        Func<InboundMessage, CancellationToken, Task<AckAction>> onMessage,
        CancellationToken cancellationToken = default);
}

