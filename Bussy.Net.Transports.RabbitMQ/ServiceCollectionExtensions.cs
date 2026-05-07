using Bussy.Net.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace Bussy.Net.Transports.RabbitMQ;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBussyRabbitMqTransport(
        this IServiceCollection services,
        Action<BussyConfigurator> configure,
        Action<RabbitMqTransportOptions>? configureRabbitMq = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new RabbitMqTransportOptions();
        configureRabbitMq?.Invoke(options);

        services.AddBussy(configure);
        services.AddSingleton(options);
        services.AddSingleton<IRabbitMqMessageMapper, RabbitMqMessageMapper>();
        services.AddSingleton<ITransport, RabbitMqTransport>();

        return services;
    }
}

