using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Bussy.Net.Registries;

internal class HandlerRegistry(MessageRouteResolver routeResolver, IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
{
    public ConcurrentDictionary<MessageRoute, IEnumerable<IInboundMessageHandler>> Handlers { get; } = new();

    public void RegisterHandler<THandler, TMessage>(string? topic = null, string? broker = null) 
        where THandler : IHandler<TMessage>
    {
        var typeRoute = routeResolver.Resolve(typeof(TMessage));
        topic ??= typeRoute.Topic;
        broker ??= typeRoute.Broker;
        
        var route = new MessageRoute(Topic: topic, Broker: broker);
        
        Handlers.AddOrUpdate(
            route, 
            _ => [
                new InboundMessageHandler<TMessage>(serviceProvider, typeof(THandler), loggerFactory.CreateLogger<InboundMessageHandler<TMessage>>())
            ],
            (_, old) => old.Append(
                new InboundMessageHandler<TMessage>(serviceProvider, typeof(THandler), loggerFactory.CreateLogger<InboundMessageHandler<TMessage>>())
            )
        );
    }
}