using Bussy.Net.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace Bussy.Net.Transports.InMemory;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBussyInMemoryTransport(this IServiceCollection services, Action<BussyConfigurator> configure)
    {
        services.AddBussy(configure);
        
        services.AddSingleton<ITransport, InMemoryTransport>();
        return services;
    }
}