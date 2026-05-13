using Bussy.Net.Transports.RabbitMq;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace Bussy.Net.Test.E2E;

public class RabbitMqEndToEndTests : EndToEndTestFixture
{
#pragma warning disable NUnit1032
    private IContainer _rabbitMqContainer = null!;
#pragma warning restore NUnit1032
    private IConnection? _cachedConnection;
    private static string RabbitMqConfigPath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "rabbitmq.conf"));

    protected override async Task StartExternalDependenciesAsync()
    {
        await base.StartExternalDependenciesAsync();

        _rabbitMqContainer = new ContainerBuilder()
            .WithImage("rabbitmq:3.13-management")
            .WithBindMount(RabbitMqConfigPath, "/etc/rabbitmq.conf")
            .WithEnvironment("RABBITMQ_CONFIG_FILE", "/etc/rabbitmq.conf")
            .WithPortBinding(5672, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilInternalTcpPortIsAvailable(5672)
                .UntilCommandIsCompleted("rabbitmq-diagnostics -q check_running"))
            .Build();

        await _rabbitMqContainer.StartAsync();
        await WaitForRabbitMqReadyAsync();

        // Pre-initialize the connection asynchronously since DI factories are synchronous.
        var factory = new ConnectionFactory
        {
            HostName = _rabbitMqContainer.Hostname,
            Port = _rabbitMqContainer.GetMappedPublicPort(5672),
            UserName = "bussy",
            Password = "bussy",
            VirtualHost = "/",
        };
        _cachedConnection = await factory.CreateConnectionAsync();
    }

    private async Task WaitForRabbitMqReadyAsync(CancellationToken cancellationToken = default)
    {
        var factory = new ConnectionFactory
        {
            HostName = _rabbitMqContainer.Hostname,
            Port = _rabbitMqContainer.GetMappedPublicPort(5672),
            UserName = "bussy",
            Password = "bussy",
            VirtualHost = "/",
        };

        var timeoutAt = DateTime.UtcNow.AddSeconds(30);
        Exception? lastError = null;

        while (DateTime.UtcNow < timeoutAt)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await using var connection = await factory.CreateConnectionAsync(cancellationToken: cancellationToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

                await channel.ExchangeDeclareAsync(
                    exchange: "bussy.readiness.probe",
                    type: ExchangeType.Fanout,
                    durable: false,
                    autoDelete: true,
                    cancellationToken: cancellationToken);

                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
        }

        throw new TimeoutException("RabbitMQ was not ready to accept AMQP operations within 30 seconds.", lastError);
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
        services.AddBussyRabbitMqTransport(configure =>
        {
            configure.RegisterHandler<E2ETestMessageHandler, E2ETestMessage>();
        });
    }
}
