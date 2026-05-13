using Bussy.Net.Transport;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Bussy.Net.Transports.RabbitMq;

public interface IRabbitMqMessageMapper
{
    BasicProperties MapOutbound(OutboundMessage message);

    InboundMessage MapInbound(BasicDeliverEventArgs eventArgs, string transportName);
}



