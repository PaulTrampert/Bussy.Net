using System;

namespace Bussy.Net;

public record MessageContext<TMessage>(
    Guid Id,
    DateTime SendTime,
    DateTime ReceiveTime,
    string Topic,
    string Broker,
    TMessage Message
);