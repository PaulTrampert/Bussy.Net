using System.Reflection;
using Bussy.Net.Registries;
using Bussy.Net.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace Bussy.Net;

public class BussyConfigurator
{
    private readonly HandlerRegistry _handlerRegistry;
    private readonly TransportRegistry _transportRegistry;
    private readonly IServiceProvider _serviceProvider;

    internal BussyConfigurator(HandlerRegistry handlerRegistry, TransportRegistry transportRegistry, IServiceProvider serviceProvider)
    {
        _handlerRegistry = handlerRegistry;
        _transportRegistry = transportRegistry;
        _serviceProvider = serviceProvider;
    }

    public void DetectHandlers(params Assembly[] assemblies)
    {
        assemblies = assemblies.Length > 0 ? assemblies : AppDomain.CurrentDomain.GetAssemblies();
        var handlerTypes = assemblies.SelectMany(
            assembly => assembly.GetTypes()
                .Where(type => type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHandler<>)))
        );

        foreach (var handlerType in handlerTypes)
        {
            _handlerRegistry.RegisterHandler(handlerType);
        }
    }

    public void RegisterHandler<THandler, TMessage>(string? topic = null, string? broker = null) where THandler : IHandler<TMessage>
    {
        _handlerRegistry.RegisterHandler<THandler, TMessage>(topic, broker);
    }

    public void RegisterTransports()
    {
        var transports = _serviceProvider.GetServices<ITransport>();
        foreach (var transport in transports)
        {
            _transportRegistry.Register(transport);
        }
    }
}