using Bussy.Net.Registries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bussy.Net;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBussy(this IServiceCollection services, Action<BussyConfigurator> configure)
    {
        services.AddSingleton<HandlerRegistry>();
        services.AddSingleton<TransportRegistry>();
        services.AddSingleton<MessageRouteResolver>();
        services.AddSingleton<IHostedService, BussyService>();
        services.AddScoped<IPublisher, DefaultPublisher>();
        
        services.Configure<BussyConfigurator>(cfg =>
        {
            configure(cfg);
            cfg.RegisterTransports();
        });
        
        return services;
    }
}