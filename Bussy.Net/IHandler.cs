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
    /// <param name="message">The parsed message object.</param>
    /// <returns>Task that will resolve when processing is complete</returns>
    Task HandleAsync(MessageContext<TMessage> context);
}