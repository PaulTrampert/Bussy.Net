using Bussy.Net.Transport;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Bussy.Net.Transports.RabbitMq;

/// <summary>
/// Maps between Bussy.Net message envelopes and RabbitMQ AMQP primitives.
/// </summary>
public interface IRabbitMqMessageMapper
{
    /// <summary>
    /// Maps an <see cref="OutboundMessage"/> to a <see cref="BasicProperties"/> instance
    /// ready to be passed to a RabbitMQ channel publish call.
    /// </summary>
    /// <param name="message">The outbound message to map.</param>
    /// <returns>A populated <see cref="BasicProperties"/> object.</returns>
    BasicProperties MapOutbound(OutboundMessage message);

    /// <summary>
    /// Maps a RabbitMQ delivery event to an <see cref="InboundMessage"/>.
    /// </summary>
    /// <param name="eventArgs">The delivery event arguments received from the RabbitMQ consumer.</param>
    /// <param name="transportName">The logical name of the transport, used as the broker identifier.</param>
    /// <returns>An <see cref="InboundMessage"/> containing the deserialized payload and metadata.</returns>
    InboundMessage MapInbound(BasicDeliverEventArgs eventArgs, string transportName);
}



