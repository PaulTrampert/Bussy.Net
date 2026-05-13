using System.Reflection;
using Bussy.Net.Helpers;
using Bussy.Net.Registries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bussy.Net;

/// <summary>
/// Extension methods for registering Bussy.Net services with the <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all core Bussy.Net services and applies the supplied configuration.
    /// Call this method (or a transport-specific overload that delegates to it) once during application startup.
    /// Any <see cref="IHandler{TMessage}"/> implementations already registered in the <see cref="IServiceCollection"/>
    /// are automatically subscribed — no explicit <see cref="BussyConfigurator.RegisterHandler{THandler,TMessage}"/> call is needed.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">
    /// An optional delegate for additional configuration (e.g. handlers with non-default routes or explicit broker targeting).
    /// When <see langword="null"/>, only handlers discovered from the service collection are registered.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> instance so calls can be chained.</returns>
    public static IServiceCollection AddBussy(this IServiceCollection services, Action<BussyConfigurator>? configure = null)
    {
        services.AddSingleton<HandlerRegistry>();
        services.AddSingleton<TransportRegistry>();
        services.AddSingleton<MessageRouteResolver>();
        services.AddSingleton<IHostedService, BussyService>();
        services.AddSingleton<BussyConfigurator>(sp =>
        {
            var configurator = new BussyConfigurator(
                sp.GetRequiredService<HandlerRegistry>(),
                sp.GetRequiredService<TransportRegistry>(),
                sp);
            configurator.RegisterTransports();
            configure?.Invoke(configurator);

            // Auto-subscribe every IHandler<> implementation that has been registered in the DI container.
            // Handlers already registered via the configure callback are skipped to avoid duplicate subscriptions.
            var discoveredHandlerTypes = services
                .Select(GetConcreteImplementationType)
                .OfType<Type>()
                .Where(t => t.GetInterfaces()
                    .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHandler<>)))
                .Distinct();

            foreach (var handlerType in discoveredHandlerTypes)
            {
                if (!configurator.HandlerRegistry.IsHandlerRegistered(handlerType))
                {
                    configurator.HandlerRegistry.RegisterHandler(handlerType);
                }
            }

            return configurator;
        });
        services.AddScoped<IPublisher, DefaultPublisher>();
        
        return services;
    }

    /// <summary>
    /// Returns the concrete implementation type for a <see cref="ServiceDescriptor"/>, or
    /// <see langword="null"/> when the type cannot be statically determined (e.g. factory or instance registrations
    /// where the service type is an interface or abstract class).
    /// </summary>
    private static Type? GetConcreteImplementationType(ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationType != null)
            return descriptor.ImplementationType;

        // Factory- and instance-based registrations don't expose a static implementation type,
        // but when the service is registered directly by its concrete class (e.g. services.AddScoped<MyHandler>()),
        // the ServiceType *is* the concrete type.
        if (descriptor.ImplementationFactory == null
            && descriptor.ImplementationInstance == null
            && !descriptor.ServiceType.IsInterface
            && !descriptor.ServiceType.IsAbstract)
        {
            return descriptor.ServiceType;
        }

        return null;
    }

    /// <summary>
    /// Scans the given assemblies for <see cref="IHandler{TMessage}"/> implementations and registers each as a
    /// scoped service using its concrete type. When used together with <see cref="AddBussy"/>, the discovered
    /// handlers are automatically subscribed — no explicit
    /// <see cref="BussyConfigurator.RegisterHandler{THandler,TMessage}"/> call is needed.
    /// If no assemblies are provided, all assemblies currently loaded in the application domain are scanned.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="assemblies">The assemblies to scan. When empty, all loaded assemblies are used.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance so calls can be chained.</returns>
    public static IServiceCollection AddBussyHandlers(this IServiceCollection services, params Assembly[] assemblies)
    {
        assemblies = assemblies.Length > 0 ? assemblies : AppDomain.CurrentDomain.GetAssemblies();
        var handlerTypes = assemblies.SelectMany(
            assembly => assembly.GetLoadableTypes()
                .Where(type => type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHandler<>)))
        );

        foreach (var handlerType in handlerTypes)
        {
            services.AddScoped(handlerType);
        }
        
        return services;
    }
}