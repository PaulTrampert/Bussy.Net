using Bussy.Net.Transports.InMemory;
using Microsoft.Extensions.DependencyInjection;

namespace Bussy.Net.Test.E2E;

public class InMemoryEndToEndTests : EndToEndTestFixture
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddBussyInMemoryTransport(configure =>
        {
            configure.RegisterHandler<E2ETestMessageHandler, E2ETestMessage>();
        });
    }
}