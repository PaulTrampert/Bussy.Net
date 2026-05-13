using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bussy.Net.Test.E2E;

[TestFixture]
public abstract class EndToEndTestFixture
{
    private IHost _host = null!;

    protected IServiceProvider Services => _host.Services;
    protected IPublisher Publisher;
    protected IServiceScope Scope;
    
    protected static readonly ConcurrentBag<E2ETestMessage> HandledE2ETestMessages = new();

    protected virtual Task StartExternalDependenciesAsync()
    {
        return Task.CompletedTask;
    }

    protected virtual Task StopExternalDependenciesAsync()
    {
        return Task.CompletedTask;
    }

    protected abstract void ConfigureServices(IServiceCollection services);

    [OneTimeSetUp]
    public async Task TestFixtureSetup()
    {
        await StartExternalDependenciesAsync();

        var hostBuilder = Host.CreateDefaultBuilder();

        hostBuilder.ConfigureServices(sc =>
        {
            ConfigureServices(sc);
            sc.AddScoped<E2ETestMessageHandler>();
        });

        _host = hostBuilder.Build();
        await _host.StartAsync();
    }

    [SetUp]
    public void Setup()
    {
        HandledE2ETestMessages.Clear();
        Scope = Services.CreateScope();
        Publisher = Scope.ServiceProvider.GetRequiredService<IPublisher>();
    }

    [TearDown]
    public void TearDown()
    {
        Scope.Dispose();
    }

    [OneTimeTearDown]
    public async Task TestFixtureTeardown()
    {
        await _host.StopAsync();
        
        if (_host is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            _host.Dispose();
        }

        await StopExternalDependenciesAsync();
    }

    [Test]
    public async Task PublishedMessage_IsHandled()
    {
        var message = new E2ETestMessage("Hello World");

        await Publisher.PublishAsync(message);

        E2ETestMessage? handledMessage = null;
        Assert.That(() => HandledE2ETestMessages.TryTake(out handledMessage), Is.True.After(30000, 100));
        Assert.That(handledMessage, Is.EqualTo(message));
    }

    protected sealed record E2ETestMessage(string Value);

    protected sealed class E2ETestMessageHandler : IHandler<E2ETestMessage>
    {
        public Task HandleAsync(MessageContext<E2ETestMessage> context, CancellationToken cancellationToken = default)
        {
            HandledE2ETestMessages.Add(context.Message);
            return Task.CompletedTask;
        }
    }
}
