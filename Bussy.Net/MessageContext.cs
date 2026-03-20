using System;

namespace Bussy.Net;

/// <summary>
/// Context object that is passed to handlers when a message is received. Contains metadata about the message, such as the topic and broker it was received from, as well as the message itself.
/// </summary>
/// <param name="Id">Message Id</param>
/// <param name="SendTime">Timestamp when the message was sent (UTC)</param>
/// <param name="ReceiveTime">Timestamp when the message was received (UTC)</param>
/// <param name="Topic">The topic the message was published to</param>
/// <param name="Broker">The broker the message was received from</param>
/// <param name="Message">The message</param>
/// <typeparam name="TMessage">The message type</typeparam>
public record MessageContext<TMessage>(
    Guid Id,
    DateTime SendTime,
    DateTime ReceiveTime,
    string Topic,
    string Broker,
    TMessage Message
);