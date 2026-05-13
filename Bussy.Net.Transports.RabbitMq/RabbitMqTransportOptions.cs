namespace Bussy.Net.Transports.RabbitMq;

/// <summary>
/// Configuration options for the RabbitMQ transport.
/// </summary>
public sealed class RabbitMqTransportOptions
{
    /// <summary>
    /// Gets or sets the logical name used to identify this transport instance.
    /// Defaults to <c>"rabbitmq"</c>.
    /// </summary>
    public string TransportName { get; set; } = "rabbitmq";
}
