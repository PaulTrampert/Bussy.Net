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
    Task SendAsync(OutboundMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a batch of messages to the same transport.
    /// </summary>
    Task SendBatchAsync(IReadOnlyCollection<OutboundMessage> messages, CancellationToken cancellationToken = default);
}

