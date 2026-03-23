using System.Threading.Tasks;

namespace Bussy.Net;

/// <summary>
/// Interface to be implemented by consumers to handle incoming messages.
/// </summary>
/// <typeparam name="TMessage">The type of message this handler consumes.</typeparam>
public interface IHandler<TMessage>
{
    /// <summary>
    /// Handles a message received from the message broker.
    /// </summary>
    /// <param name="context">The handler context, along with the parsed message.</param>
    /// <param name="cancellationToken">Cancellation token to signal when processing should be aborted.</param>
    /// <returns>Task that will resolve when processing is complete</returns>
    Task HandleAsync(MessageContext<TMessage> context, CancellationToken cancellationToken = default);
}