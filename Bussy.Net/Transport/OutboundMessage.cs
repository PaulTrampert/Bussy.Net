using System;
using System.Collections.Generic;

namespace Bussy.Net.Transport;

/// <summary>
/// Serialized message payload and metadata ready to be sent by a transport adapter.
/// </summary>
/// <param name="Body">Serialized message body bytes.</param>
/// <param name="Topic">Destination topic, queue, or route name.</param>
/// <param name="Broker">Logical broker identifier selected by the caller or resolver.</param>
/// <param name="Headers">Metadata headers that should travel with the message.</param>
/// <param name="MessageId">Stable message id used for traceability and deduplication.</param>
/// <param name="SentAtUtc">UTC timestamp when the message was created for transport.</param>
public sealed record OutboundMessage(
    ReadOnlyMemory<byte> Body,
    string Topic,
    string Broker,
    IReadOnlyDictionary<string, string?> Headers,
    Guid MessageId,
    DateTimeOffset SentAtUtc
);
