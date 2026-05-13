using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Bussy.Net.Transport;

/// <summary>
/// Sends serialized messages to a backing broker implementation.
/// </summary>
public interface ITransportSender
{
    /// <summary>
    /// Sends a single message.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the message has been handed off to the broker.</returns>
    Task SendAsync(OutboundMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a batch of messages to the same transport.
    /// </summary>
    /// <param name="messages">The collection of messages to send.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when all messages have been handed off to the broker.</returns>
    Task SendBatchAsync(IReadOnlyCollection<OutboundMessage> messages, CancellationToken cancellationToken = default);
}

