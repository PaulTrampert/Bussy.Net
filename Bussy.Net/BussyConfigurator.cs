using System.Reflection;
using Bussy.Net.Helpers;
using Bussy.Net.Registries;
using Bussy.Net.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace Bussy.Net;

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