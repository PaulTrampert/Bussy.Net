using Bussy.Net.Transport;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Bussy.Net.Transports.RabbitMQ;

public sealed class RabbitMqTransport(
    RabbitMqTransportOptions options,
    IChannel rabbitmqChannel,
    IRabbitMqMessageMapper messageMapper) : ITransport
{
    private readonly RabbitMqTransportOptions _options = options;
    private readonly IRabbitMqMessageMapper _messageMapper = messageMapper;

    public string Name => _options.TransportName;

    public TransportCapability Capabilities => TransportCapability.None;

    public async Task SendAsync(OutboundMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        await rabbitmqChannel.ExchangeDeclareAsync(
            exchange: message.Topic,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        var properties = _messageMapper.MapOutbound(message);

        await rabbitmqChannel.BasicPublishAsync(
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

        await rabbitmqChannel.ExchangeDeclareAsync(
            exchange: topic,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        var queueName = $"{Name}.{topic}.{handler.Name}";
        await rabbitmqChannel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await rabbitmqChannel.QueueBindAsync(
            queue: queueName,
            exchange: topic,
            routingKey: string.Empty,
            cancellationToken: cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(rabbitmqChannel);
        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            var inbound = _messageMapper.MapInbound(eventArgs, Name);
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
                    await rabbitmqChannel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken);
                    break;
                case AckAction.DeadLetter:
                    await rabbitmqChannel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false,
                        cancellationToken);
                    break;
                case AckAction.Retry:
                default:
                    await rabbitmqChannel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true,
                        cancellationToken);
                    break;
            }
        };

        var consumerTag = await rabbitmqChannel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);

        return new RabbitMqTransportSubscription(
            $"{Name}_{topic}_{handler.Name}",
            () => new ValueTask(rabbitmqChannel.BasicCancelAsync(consumerTag)));
    }
}
