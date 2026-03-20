using System;

namespace Bussy.Net.Transport;

/// <summary>
/// Optional transport features exposed by a broker adapter.
/// </summary>
[Flags]
public enum TransportCapability
{
    None = 0,
    BatchSend = 1 << 0,
    DelayedDelivery = 1 << 1,
    OrderedPartitions = 1 << 2,
    NativeDeadLetter = 1 << 3,
    Transactions = 1 << 4
}

