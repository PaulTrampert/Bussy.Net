using System.Reflection;
using Bussy.Net.Helpers;
using Bussy.Net.Registries;
using Bussy.Net.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace Bussy.Net;

/// <summary>
/// Provides configuration methods for registering message handlers and transports with the Bussy.Net messaging infrastructure.
/// </summary>
public class BussyConfigurator
{
    internal readonly HandlerRegistry HandlerRegistry;
    internal readonly TransportRegistry TransportRegistry;
    private readonly IServiceProvider _serviceProvider;

    internal BussyConfigurator(HandlerRegistry handlerRegistry, TransportRegistry transportRegistry, IServiceProvider serviceProvider)
    {
        HandlerRegistry = handlerRegistry;
        TransportRegistry = transportRegistry;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Scans the given assemblies for <see cref="IHandler{TMessage}"/> implementations and registers them automatically.
    /// If no assemblies are provided, all assemblies currently loaded in the application domain are scanned.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan. When empty, all loaded assemblies are used.</param>
    public void DetectHandlers(params Assembly[] assemblies)
    {
        assemblies = assemblies.Length > 0 ? assemblies : AppDomain.CurrentDomain.GetAssemblies();
        var handlerTypes = assemblies.SelectMany(
            assembly => assembly.GetLoadableTypes()
                .Where(type => type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHandler<>)))
        );

        foreach (var handlerType in handlerTypes)
        {
            HandlerRegistry.RegisterHandler(handlerType);
        }
    }

    /// <summary>
    /// Registers a specific handler type for a specific message type, optionally scoped to a topic and/or broker.
    /// </summary>
    /// <typeparam name="THandler">The handler type that processes <typeparamref name="TMessage"/>.</typeparam>
    /// <typeparam name="TMessage">The message type consumed by the handler.</typeparam>
    /// <param name="topic">Optional topic override. When <see langword="null"/>, the default topic for <typeparamref name="TMessage"/> is used.</param>
    /// <param name="broker">Optional broker override. When <see langword="null"/>, the default broker for <typeparamref name="TMessage"/> is used.</param>
    public void RegisterHandler<THandler, TMessage>(string? topic = null, string? broker = null) where THandler : IHandler<TMessage>
    {
        HandlerRegistry.RegisterHandler<THandler, TMessage>(topic, broker);
    }

    internal void RegisterTransports()
    {
        var transports = _serviceProvider.GetServices<ITransport>();
        foreach (var transport in transports)
        {
            TransportRegistry.Register(transport);
        }
    }
}