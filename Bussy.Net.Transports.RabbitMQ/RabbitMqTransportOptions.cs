namespace Bussy.Net.Transports.RabbitMQ;

public sealed class RabbitMqTransportOptions
{
    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string Username { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public string VirtualHost { get; set; } = "/";

    public string TransportName { get; set; } = "rabbitmq";
}

