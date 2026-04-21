using Bussy.Net.Transports.RabbitMQ;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.DependencyInjection;

namespace Bussy.Net.Test.E2E;

[Explicit("RabbitMQ transport is scaffolded but not implemented yet.")]
public class RabbitMQEndToEndTests : EndToEndTestFixture
{
    private IContainer _rabbitMqContainer = null!;

    protected override void StartExternalDependencies()
    {
        base.StartExternalDependencies();

        _rabbitMqContainer = new ContainerBuilder()
            .WithImage("rabbitmq:3.13-management")
            .WithPortBinding(5672, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(5672))
            .Build();

        _rabbitMqContainer.StartAsync().GetAwaiter().GetResult();
    }

    protected override async Task StopExternalDependenciesAsync()
    {
        if (_rabbitMqContainer is not null)
        {
            await _rabbitMqContainer.DisposeAsync();
        }

        await base.StopExternalDependenciesAsync();
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
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