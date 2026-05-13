using Bussy.Net.Transports.RabbitMQ;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace Bussy.Net.Test.E2E;

[Explicit("RabbitMQ transport is scaffolded but not implemented yet.")]
public class RabbitMQEndToEndTests : EndToEndTestFixture
{
    private IContainer _rabbitMqContainer = null!;
    private IConnection? _cachedConnection;

    protected override async Task StartExternalDependenciesAsync()
    {
        await base.StartExternalDependenciesAsync();

        _rabbitMqContainer = new ContainerBuilder()
            .WithImage("rabbitmq:3.13-management")
            .WithPortBinding(5672, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(5672))
            .Build();

        await _rabbitMqContainer.StartAsync();

        // Pre-initialize the connection asynchronously since DI factories are synchronous.
        var factory = new ConnectionFactory
        {
            HostName = _rabbitMqContainer.Hostname,
            Port = _rabbitMqContainer.GetMappedPublicPort(5672),
            UserName = "guest",
            Password = "guest",
            VirtualHost = "/",
        };
        _cachedConnection = await factory.CreateConnectionAsync();
    }

    protected override async Task StopExternalDependenciesAsync()
    {
        if (_cachedConnection is not null)
        {
            _cachedConnection.Dispose();
        }

        if (_rabbitMqContainer is not null)
        {
            await _rabbitMqContainer.DisposeAsync();
        }

        await base.StopExternalDependenciesAsync();
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        // Provide the pre-initialized async connection.
        services.AddSingleton<IConnection>(_ => _cachedConnection ?? throw new InvalidOperationException("Connection not initialized"));

        // Channel is NOT thread-safe; scoped is usually safest for request/message pipeline usage.
        services.AddScoped<IChannel>(sp =>
        {
            var connection = sp.GetRequiredService<IConnection>();
            return connection.CreateChannelAsync().GetAwaiter().GetResult();
        });

        services.AddBussyRabbitMqTransport(configure =>
        {
            configure.RegisterHandler<E2ETestMessageHandler, E2ETestMessage>();
        }, rabbitMq =>
        {
            rabbitMq.Host = _rabbitMqContainer.Hostname;
            rabbitMq.Port = _rabbitMqContainer.GetMappedPublicPort(5672);
        });
    }
}