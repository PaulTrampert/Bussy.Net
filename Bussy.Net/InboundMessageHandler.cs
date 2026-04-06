using System.Text;
using System.Text.Json;
using Bussy.Net.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bussy.Net;

public class InboundMessageHandler<T> : IInboundMessageHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Type _handlerType;
    private readonly ILogger<InboundMessageHandler<T>> _logger;

    public InboundMessageHandler(IServiceProvider serviceProvider, Type handlerType, ILogger<InboundMessageHandler<T>> logger)
    {
        if (!typeof(IHandler<T>).IsAssignableFrom(handlerType))
        {
            throw new ArgumentException($"Handler {handlerType.FullName} does not implement {typeof(IHandler<T>).FullName}.", nameof(handlerType));
        }
        _serviceProvider = serviceProvider;
        _handlerType = handlerType;
        _logger = logger;
    }


    public async Task<AckAction> HandleInboundMessageAsync(InboundMessage message, CancellationToken token)
    {
        IHandler<T> handler;
        MessageContext<T> messageContext;
        using var scope = _serviceProvider.CreateScope();
        try
        {
            var messageObj = JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(message.Body.Span));
            if (messageObj == null)
            {
                throw new MessageNullException();
            }

            handler = ActivatorUtilities.CreateInstance(scope.ServiceProvider, _handlerType) as IHandler<T>
                      ?? throw new InvalidOperationException($"Failed to create handler instance of type {_handlerType.FullName}.");

            messageContext = new MessageContext<T>(
                message.MessageId,
                message.SentAtUtc,
                message.ReceivedAtUtc,
                message.Topic,
                message.Broker,
                message.DeliveryAttempt,
                message.Headers,
                messageObj);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unprocessable message: {@Message}", message);
            return AckAction.DeadLetter;
        }

        try
        {
            await handler.HandleAsync(messageContext, token);
        }
        catch (OperationCanceledException oce)
        {
            _logger.LogWarning(oce, "Message processing cancelled: {@MessageContext}", messageContext);
            return AckAction.Retry;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error processing message: {@MessageContext}", messageContext);
            return AckAction.Retry;
        }
        finally
        {
            if (handler is IAsyncDisposable asyncDisposableHandler)
            {
                await asyncDisposableHandler.DisposeAsync();
            }
            else if (handler is IDisposable disposableHandler)
            {
                disposableHandler.Dispose();
            }
        }
        return AckAction.Ack;
    }
}

public class MessageNullException() : Exception("Message deserialized as null");

public interface IInboundMessageHandler
{
    Task<AckAction> HandleInboundMessageAsync(InboundMessage message, CancellationToken token);
}