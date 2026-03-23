using System;
using System.Collections.Generic;

namespace Bussy.Net.Transport;

/// <summary>
/// Serialized message payload and metadata as delivered from a transport adapter.
/// </summary>
/// <param name="Body">Serialized message body bytes.</param>
/// <param name="Topic">Source topic, queue, or route name.</param>
/// <param name="Broker">Logical broker identifier used for this delivery.</param>
/// <param name="Headers">Metadata headers attached to this message.</param>
/// <param name="MessageId">Stable message id used for traceability and deduplication.</param>
/// <param name="SentAtUtc">UTC timestamp when the producer sent the message.</param>
/// <param name="ReceivedAtUtc">UTC timestamp when the adapter received the message.</param>
/// <param name="DeliveryAttempt">Delivery count when the backend exposes one, otherwise 1.</param>
public sealed record InboundMessage(
    ReadOnlyMemory<byte> Body,
    string Topic,
    string Broker,
    IReadOnlyDictionary<string, string?> Headers,
    Guid MessageId,
    DateTimeOffset SentAtUtc,
    DateTimeOffset ReceivedAtUtc,
    int DeliveryAttempt
);

