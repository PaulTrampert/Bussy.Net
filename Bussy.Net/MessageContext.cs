namespace Bussy.Net;

/// <summary>
/// Context object that is passed to handlers when a message is received. Contains metadata about the message, such as the topic and broker it was received from, as well as the message itself.
/// </summary>
/// <param name="Id">Message Id</param>
/// <param name="SentAtUtc">Timestamp when the message was sent (UTC)</param>
/// <param name="ReceivedAtUtc">Timestamp when the message was received (UTC)</param>
/// <param name="Topic">The topic the message was published to</param>
/// <param name="Broker">The broker the message was received from</param>
/// <param name="DeliveryAttempt">Number of times delivery has been attempted for this message</param>
/// <param name="Headers">Metadata headers attached to the message</param>
/// <param name="Message">The message</param>
/// <typeparam name="TMessage">The message type</typeparam>
public record MessageContext<TMessage>(
    Guid Id,
    DateTimeOffset SentAtUtc,
    DateTimeOffset ReceivedAtUtc,
    string Topic,
    string Broker,
    int DeliveryAttempt,
    IReadOnlyDictionary<string, string?> Headers,
    TMessage Message
);