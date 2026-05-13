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
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">A delegate that configures handlers and transports via <see cref="BussyConfigurator"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance so calls can be chained.</returns>
    public static IServiceCollection AddBussy(this IServiceCollection services, Action<BussyConfigurator> configure)
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
            configure(configurator);
            return configurator;
        });
        services.AddScoped<IPublisher, DefaultPublisher>();
        
        return services;
    }

    /// <summary>
    /// Registers all <see cref="IHandler{TMessage}"/> implementations found in the given assemblies as scoped services.
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