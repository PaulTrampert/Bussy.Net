using Bussy.Net.Registries;
using Bussy.Net.Transport;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bussy.Net;

internal class BussyService(BussyConfigurator bussyConfigurator, ILogger<BussyService> logger) : BackgroundService
{
    private readonly List<ITransportSubscription> _subscriptions = [];
    private readonly HandlerRegistry _handlerRegistry = bussyConfigurator.HandlerRegistry;
    private readonly TransportRegistry _transportRegistry = bussyConfigurator.TransportRegistry;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var (route, handlers) in _handlerRegistry.Handlers)
        {
            var handlerList = handlers.ToList();
            IEnumerable<ITransport> transports = route.Broker is not null
                ? _transportRegistry.Transports.TryGetValue(route.Broker, out var t) ? [t] : []
                : _transportRegistry.Transports.Values;

            foreach (var transport in transports)
            foreach (var handler in handlerList)
            {
                var subscription = await transport.SubscribeAsync(
                    route.Topic,
                    handler.HandleInboundMessageAsync,
                    stoppingToken);

                _subscriptions.Add(subscription);
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            }
            catch (OperationCanceledException e)
            {
                if (e.CancellationToken == stoppingToken)
                {
                    logger.LogInformation("Shutting down BussyService");
                }
                else
                {
                    logger.LogError(e, "Unexpected cancellation requested, shutting down BussyService");
                }

                break;
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var subscription in _subscriptions)
            await subscription.DisposeAsync();

        await base.StopAsync(cancellationToken);
    }
}