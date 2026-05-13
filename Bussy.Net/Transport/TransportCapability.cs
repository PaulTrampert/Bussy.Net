using System;

namespace Bussy.Net.Transport;

/// <summary>
/// Optional transport features exposed by a broker adapter.
/// </summary>
[Flags]
public enum TransportCapability
{
    /// <summary>No optional capabilities.</summary>
    None = 0,

    /// <summary>The transport can send multiple messages in a single batched call.</summary>
    BatchSend = 1 << 0,

    /// <summary>The transport supports scheduling a message for future delivery.</summary>
    DelayedDelivery = 1 << 1,

    /// <summary>The transport preserves message order within a partition or shard.</summary>
    OrderedPartitions = 1 << 2,

    /// <summary>The transport natively routes undeliverable messages to a dead-letter destination.</summary>
    NativeDeadLetter = 1 << 3,

    /// <summary>The transport supports transactional send operations.</summary>
    Transactions = 1 << 4
}

