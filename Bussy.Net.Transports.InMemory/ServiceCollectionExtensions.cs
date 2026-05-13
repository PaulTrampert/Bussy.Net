using Bussy.Net.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace Bussy.Net.Transports.InMemory;

/// <summary>
/// Extension methods for registering the in-memory Bussy.Net transport with the <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Bussy.Net core services together with the in-memory transport.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">A delegate that configures handlers and transports via <see cref="BussyConfigurator"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance so calls can be chained.</returns>
    public static IServiceCollection AddBussyInMemoryTransport(this IServiceCollection services, Action<BussyConfigurator> configure)
    {
        services.AddBussy(configure);
        
        services.AddSingleton<ITransport, InMemoryTransport>();
        return services;
    }
}