using System.Collections.Concurrent;
using Bussy.Net.Transport;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Bussy.Net.Transports.RabbitMq;

public sealed class RabbitMqTransport(
    RabbitMqTransportOptions options,
    IConnection connection,
    IRabbitMqMessageMapper messageMapper,
    ILogger<RabbitMqTransport> logger) : ITransport, IAsyncDisposable
{
    private const byte DeclaredExchangeMarker = byte.MinValue;
    private readonly SemaphoreSlim _publisherChannelLock = new(1, 1);
    private readonly SemaphoreSlim _publisherSendLock = new(1, 1);
    private readonly ConcurrentDictionary<string, byte> _declaredExchanges = new(StringComparer.Ordinal);
    private IChannel? _publisherChannel;
    private int _disposed;

    public string Name => options.TransportName;

    public TransportCapability Capabilities => TransportCapability.None;

    public async Task SendAsync(OutboundMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ThrowIfDisposed();

        var channel = await GetOrCreatePublisherChannelAsync(cancellationToken);
        var properties = messageMapper.MapOutbound(message);

        await _publisherSendLock.WaitAsync(cancellationToken);
        try
        {
            if (_declaredExchanges.TryAdd(message.Topic, DeclaredExchangeMarker))
            {
                await channel.ExchangeDeclareAsync(
                    exchange: message.Topic,
                    type: ExchangeType.Fanout,
                    durable: true,
                    autoDelete: false,
                    cancellationToken: cancellationToken);
            }

            await channel.BasicPublishAsync(
                exchange: message.Topic,
                routingKey: string.Empty,
                mandatory: false,
                basicProperties: properties,
                body: message.Body,
                cancellationToken: cancellationToken);
        }
        finally
        {
            _publisherSendLock.Release();
        }
    }

    public async Task SendBatchAsync(IReadOnlyCollection<OutboundMessage> messages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        
        await Task.WhenAll(messages.Select(message => SendAsync(message, cancellationToken)));
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

        var subscriptionCancellation = new CancellationTokenSource();
        var subscriptionCancellationToken = subscriptionCancellation.Token;

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            var inbound = messageMapper.MapInbound(eventArgs, Name);
            AckAction action;
            try
            {
                action = await handler.HandleInboundMessageAsync(inbound, subscriptionCancellationToken);
            }
            catch (OperationCanceledException) when (subscriptionCancellationToken.IsCancellationRequested)
            {
                logger.LogDebug("Message processing canceled for subscription {SubscriptionName}.", $"{Name}_{topic}_{handler.Name}");
                return;
            }
            catch
            {
                action = AckAction.Retry;
            }

            try
            {
                switch (action)
                {
                    case AckAction.Ack:
                        await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, subscriptionCancellationToken);
                        break;
                    case AckAction.DeadLetter:
                        await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false,
                            subscriptionCancellationToken);
                        break;
                    case AckAction.Retry:
                    default:
                        await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true,
                            subscriptionCancellationToken);
                        break;
                }
            }
            catch (OperationCanceledException) when (subscriptionCancellationToken.IsCancellationRequested)
            {
                logger.LogDebug("Message acknowledgement canceled for subscription {SubscriptionName}.", $"{Name}_{topic}_{handler.Name}");
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
            channel,
            subscriptionCancellation);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await _publisherChannelLock.WaitAsync();
        try
        {
            if (_publisherChannel is not null)
            {
                await _publisherChannel.DisposeAsync();
                _publisherChannel = null;
            }
        }
        finally
        {
            _publisherChannelLock.Release();
            _publisherChannelLock.Dispose();
            _publisherSendLock.Dispose();
        }
    }

    private async Task<IChannel> GetOrCreatePublisherChannelAsync(CancellationToken cancellationToken)
    {
        var existingChannel = _publisherChannel;
        if (existingChannel is { IsOpen: true })
        {
            return existingChannel;
        }

        await _publisherChannelLock.WaitAsync(cancellationToken);
        try
        {
            existingChannel = _publisherChannel;
            if (existingChannel is { IsOpen: true })
            {
                return existingChannel;
            }

            if (existingChannel is not null)
            {
                await existingChannel.DisposeAsync();
            }

            _publisherChannel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            _declaredExchanges.Clear();
            return _publisherChannel;
        }
        finally
        {
            _publisherChannelLock.Release();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed == 1, this);
    }
}
