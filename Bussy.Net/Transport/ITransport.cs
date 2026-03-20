namespace Bussy.Net.Transport;

/// <summary>
/// Broker adapter contract composed from mandatory send/receive transport operations.
/// </summary>
public interface ITransport : ITransportSender, ITransportReceiver
{
    /// <summary>
    /// Logical transport name (for example: "in-memory", "rabbitmq", "sqs", "kafka").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Optional features supported by this transport implementation.
    /// </summary>
    TransportCapability Capabilities { get; }
}

