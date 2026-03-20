using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bussy.Net;

/// <summary>
/// Interface that allows consumers of this library to publish messages to a broker.
/// </summary>
public interface IPublisher
{
    /// <summary>
    /// Publish a message. The message will be published to the broker and topic defaults defined by the message type, including <see cref="MessageAttribute"/> overrides.
    /// </summary>
    /// <param name="message">The message to publish.</param>
    /// <typeparam name="T">The message type.</typeparam>
    /// <returns>Task that resolves when the message has been published.</returns>
    Task PublishAsync<T>(T message);
    
    /// <summary>
    /// Publish multiple messages. Messages are published to the broker and topic defaults defined by the message type, including <see cref="MessageAttribute"/> overrides.
    /// </summary>
    /// <param name="messages">The list of messages to publish.</param>
    /// <typeparam name="T">The message type.</typeparam>
    /// <returns>Task that resolves when all messages have been published.</returns>
    Task PublishAsync<T>(IEnumerable<T> messages);
    
    /// <summary>
    /// Publish a message to a specific topic. The message will be published to the broker default defined by the message type, including <see cref="MessageAttribute"/> overrides.
    /// </summary>
    /// <param name="message">The message to publish.</param>
    /// <param name="topic">The topic to publish the message on.</param>
    /// <typeparam name="T">The message type.</typeparam>
    /// <returns>Task that resolves when the message has been published.</returns>
    Task PublishAsync<T>(T message, string topic);
    
    /// <summary>
    /// Publish multiple messages to a specific topic. The messages will be published to the broker default defined by the message type, including <see cref="MessageAttribute"/> overrides.
    /// </summary>
    /// <param name="messages">The messages to publish.</param>
    /// <param name="topic">The topic to publish the messages on.</param>
    /// <typeparam name="T">The message type.</typeparam>
    /// <returns>Task that resolves when all messages have been published.</returns>
    Task PublishAsync<T>(IEnumerable<T> messages, string topic);

    /// <summary>
    /// Publish a message to a specific topic and broker. The message will be published to the specified broker and topic, ignoring any defaults defined by the message type.
    /// </summary>
    /// <param name="message">The message to publish.</param>
    /// <param name="topic">The topic to publish on.</param>
    /// <param name="broker">The broker to publish to.</param>
    /// <typeparam name="T">The message type.</typeparam>
    /// <returns>Task that resolves when the message has been published.</returns>
    Task PublishAsync<T>(T message, string topic, string broker);
    
    /// <summary>
    /// Publish multiple messages to a specific topic and broker. The messages will be published to the specified broker and topic, ignoring any defaults defined by the message type.
    /// </summary>
    /// <param name="messages">The messages to publish.</param>
    /// <param name="topic">The topic to publish the messages on.</param>
    /// <param name="broker">The broker to publish to.</param>
    /// <typeparam name="T">The message type.</typeparam>
    /// <returns>Task that resolves when all messages have been published.</returns>
    Task PublishAsync<T>(IEnumerable<T> messages, string topic, string broker);
}