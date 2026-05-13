using Bussy.Net.Transport;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Bussy.Net.Transports.RabbitMq;

public sealed class RabbitMqTransport(
    RabbitMqTransportOptions options,
    IConnection connection,
    IRabbitMqMessageMapper messageMapper) : ITransport
{
    public string Name => options.TransportName;

    public TransportCapability Capabilities => TransportCapability.None;

    public async Task SendAsync(OutboundMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: message.Topic,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        var properties = messageMapper.MapOutbound(message);

        await channel.BasicPublishAsync(
            exchange: message.Topic,
            routingKey: string.Empty,
            mandatory: false,
            basicProperties: properties,
            body: message.Body,
            cancellationToken: cancellationToken);
    }

    public Task SendBatchAsync(IReadOnlyCollection<OutboundMessage> messages, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("RabbitMQ batch send is not implemented yet.");
    }

    public Task<ITransportSubscription> SubscribeAsync(string topic, IInboundMessageHandler handler,
        CancellationToken cancellationToken = default)
    {
        return SubscribeInternalAsync(topic, handler, cancellationToken);
    }

    private async Task<ITransportSubscription> SubscribeInternalAsync(
        string topic,
        IInboundMessageHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(handler);
        
        var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: topic,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        var queueName = $"{Name}.{topic}.{handler.Name}";
        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: queueName,
            exchange: topic,
            routingKey: string.Empty,
            cancellationToken: cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            var inbound = messageMapper.MapInbound(eventArgs, Name);
            AckAction action;
            try
            {
                action = await handler.HandleInboundMessageAsync(inbound, cancellationToken);
            }
            catch
            {
                action = AckAction.Retry;
            }

            switch (action)
            {
                case AckAction.Ack:
                    await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken);
                    break;
                case AckAction.DeadLetter:
                    await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false,
                        cancellationToken);
                    break;
                case AckAction.Retry:
                default:
                    await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true,
                        cancellationToken);
                    break;
            }
        };

        var consumerTag = await channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);

        return new RabbitMqTransportSubscription(
            $"{Name}_{topic}_{handler.Name}",
            consumerTag,
            channel);
    }
}
