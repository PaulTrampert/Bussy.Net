namespace Bussy.Net.Transport;

/// <summary>
/// Normalized handler outcome that transport adapters map to broker-specific acknowledgement behavior.
/// </summary>
public enum AckAction
{
    /// <summary>
    /// Message processing succeeded.
    /// </summary>
    Ack = 0,

    /// <summary>
    /// Message should be retried (typically re-queued or made visible again).
    /// </summary>
    Retry = 1,

    /// <summary>
    /// Message should not be retried and should be moved to dead-letter storage if supported.
    /// </summary>
    DeadLetter = 2
}

