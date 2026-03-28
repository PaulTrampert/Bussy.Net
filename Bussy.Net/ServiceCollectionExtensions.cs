using Bussy.Net.Registries;
using Microsoft.Extensions.DependencyInjection;

namespace Bussy.Net;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBussy(this IServiceCollection services, Action<BussyConfigurator> configure)
    {
        services.AddSingleton<HandlerRegistry>();
        services.AddSingleton<TransportRegistry>();
        services.AddSingleton<MessageRouteResolver>();
        services.AddScoped<IPublisher, DefaultPublisher>();
        
        services.Configure(configure);
        return services;
    }
}