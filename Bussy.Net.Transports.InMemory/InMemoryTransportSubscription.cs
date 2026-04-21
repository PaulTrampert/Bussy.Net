using System.Threading.Channels;
using Bussy.Net.Transport;
using Microsoft.Extensions.Logging;

namespace Bussy.Net.Transports.InMemory;

public class InMemoryTransportSubscription : ITransportSubscription
{
    private readonly Channel<InboundMessage> _messages = Channel.CreateUnbounded<InboundMessage>();

    private readonly ILogger<InMemoryTransportSubscription> _logger;
    
    private readonly Task _processTask;
    
    internal InMemoryTransportSubscription(
        string name,
        Func<InboundMessage, CancellationToken, Task<AckAction>> processMessage,
        ILogger<InMemoryTransportSubscription> logger, 
        CancellationToken cancellationToken
    )
    {
        Name = name;
        _logger = logger;
        _processTask = ProcessAsync(processMessage, cancellationToken);
    }
    
    public string Name { get; }

    internal async Task EnqueueAsync(InboundMessage message, CancellationToken cancellationToken)
    {
        await _messages.Writer.WriteAsync(message, cancellationToken);
    }
    
    private async Task ProcessAsync(Func<InboundMessage, CancellationToken, Task<AckAction>> processMessage, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && await _messages.Reader.WaitToReadAsync(cancellationToken))
            {
                try
                {
                    if (!_messages.Reader.TryRead(out var message))
                    {
                        continue;
                    }
                    message = message with {DeliveryAttempt = message.DeliveryAttempt + 1};
                    var result = await processMessage(message, cancellationToken);
                    if (result == AckAction.Retry)
                    {
                        await _messages.Writer.WriteAsync(message, cancellationToken);
                    }

                    if (result == AckAction.DeadLetter)
                    {
                        _logger.LogError("Dead letter message: {@Message}", message);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message in subscription {SubscriptionName}", Name);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
    
    public void Dispose()
    {
        try
        {
            _messages.Writer.Complete();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown disposing of subscription {SubscriptionName}", Name);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _messages.Writer.Complete();
            await _processTask;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown disposing async of subscription {SubscriptionName}", Name);
        }
    }
}