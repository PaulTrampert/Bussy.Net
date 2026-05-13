using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Bussy.Net.Registries;

internal class HandlerRegistry(MessageRouteResolver routeResolver, IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
{
    public ConcurrentDictionary<MessageRoute, IEnumerable<IInboundMessageHandler>> Handlers { get; } = new();

    private readonly ConcurrentDictionary<Type, bool> _registeredHandlerTypes = new();

    public bool IsHandlerRegistered(Type handlerType) => _registeredHandlerTypes.ContainsKey(handlerType);

    public void RegisterHandler<THandler, TMessage>(string? topic = null, string? broker = null) 
        where THandler : IHandler<TMessage>
    {
        RegisterHandler(typeof(THandler), typeof(TMessage), topic, broker);
    }

    public void RegisterHandler(Type handlerType, string? topic = null, string? broker = null)
    {
        var messageTypes = handlerType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHandler<>))
            .Select(i => i.GetGenericArguments()[0]);

        foreach (var messageType in messageTypes)
        {
            RegisterHandler(handlerType, messageType, topic, broker);
        }
    }

    public void RegisterHandler(Type handlerType, Type messageType, string? topic, string? broker)
    {
        ArgumentNullException.ThrowIfNull(handlerType);
        ArgumentNullException.ThrowIfNull(messageType);

        var expectedHandlerInterface = typeof(IHandler<>).MakeGenericType(messageType);
        if (!expectedHandlerInterface.IsAssignableFrom(handlerType))
        {
            throw new ArgumentException(
                $"Handler {handlerType.FullName} does not implement {expectedHandlerInterface.FullName}.",
                nameof(handlerType));
        }

        var registerCore = typeof(HandlerRegistry)
            .GetMethod(nameof(RegisterHandlerCore), BindingFlags.Instance | BindingFlags.NonPublic)!;

        registerCore
            .MakeGenericMethod(messageType)
            .Invoke(this, [handlerType, topic, broker]);
    }

    private void RegisterHandlerCore<TMessage>(Type handlerType, string? topic, string? broker)
    {
        var typeRoute = routeResolver.Resolve(typeof(TMessage));
        topic ??= typeRoute.Topic;
        broker ??= typeRoute.Broker;
        
        var route = new MessageRoute(Topic: topic, Broker: broker);
        
        Handlers.AddOrUpdate(
            route, 
            _ => [
                new InboundMessageHandler<TMessage>(serviceProvider, handlerType, loggerFactory.CreateLogger<InboundMessageHandler<TMessage>>())
            ],
            (_, old) => old.Append(
                new InboundMessageHandler<TMessage>(serviceProvider, handlerType, loggerFactory.CreateLogger<InboundMessageHandler<TMessage>>())
            )
        );

        _registeredHandlerTypes.TryAdd(handlerType, true);
    }
}