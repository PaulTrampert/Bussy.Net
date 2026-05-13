using Bussy.Net.Registries;
using Bussy.Net.Test.TestMessageTypes;
using Bussy.Net.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace Bussy.Net.Test;

[TestFixture]
public sealed class ServiceCollectionExtensionsTests
{
    /// <summary>
    /// Builds a minimal service provider (without starting a hosted service) that contains
    /// the core Bussy.Net registrations so the <see cref="BussyConfigurator"/> singleton
    /// (and therefore the <see cref="HandlerRegistry"/>) can be resolved synchronously.
    /// </summary>
    private static ServiceProvider BuildProvider(Action<IServiceCollection> setup)
    {
        var services = new ServiceCollection();
        // Register a no-op ILoggerFactory so HandlerRegistry can be constructed.
        services.AddLogging();
        // Register a stub transport so BussyConfigurator.RegisterTransports does not blow up.
        var transport = new Mock<ITransport>();
        transport.Setup(t => t.Name).Returns("stub");
        services.AddSingleton(transport.Object);
        setup(services);
        var provider = services.BuildServiceProvider();
        // Resolve BussyConfigurator to trigger its lazy singleton factory, which runs
        // the configure callback and auto-discovers IHandler<> implementations.
        _ = provider.GetRequiredService<BussyConfigurator>();
        return provider;
    }

    // ---------------------------------------------------------------------------
    // Auto-discovery from DI
    // ---------------------------------------------------------------------------

    [Test]
    public void AddBussy_AutoDiscovers_HandlerRegisteredAsConcrete()
    {
        using var provider = BuildProvider(sc =>
        {
            sc.AddScoped<SimpleTestHandler>();
            sc.AddBussy();
        });

        var registry = provider.GetRequiredService<HandlerRegistry>();
        var route = new MessageRoute(Topic: nameof(TestMessage), Broker: null);

        Assert.That(registry.Handlers, Does.ContainKey(route));
        Assert.That(registry.Handlers[route], Has.Exactly(1).Items);
    }

    [Test]
    public void AddBussy_AutoDiscovers_HandlerRegisteredAsInterface()
    {
        using var provider = BuildProvider(sc =>
        {
            sc.AddScoped<IHandler<TestMessage>, SimpleTestHandler>();
            sc.AddScoped<SimpleTestHandler>(); // still needed so InboundMessageHandler can resolve by concrete type
            sc.AddBussy();
        });

        var registry = provider.GetRequiredService<HandlerRegistry>();
        var route = new MessageRoute(Topic: nameof(TestMessage), Broker: null);

        Assert.That(registry.Handlers, Does.ContainKey(route));
    }

    [Test]
    public void AddBussy_NoConfigure_NoHandlers_RegistryIsEmpty()
    {
        using var provider = BuildProvider(sc =>
        {
            sc.AddBussy();
        });

        var registry = provider.GetRequiredService<HandlerRegistry>();
        Assert.That(registry.Handlers, Is.Empty);
    }

    // ---------------------------------------------------------------------------
    // Duplicate prevention
    // ---------------------------------------------------------------------------

    [Test]
    public void AddBussy_DoesNotDuplicate_WhenHandlerAlreadyExplicitlyRegistered()
    {
        using var provider = BuildProvider(sc =>
        {
            sc.AddScoped<SimpleTestHandler>();
            sc.AddBussy(cfg =>
            {
                // Explicit registration for the same handler with the same default route.
                cfg.RegisterHandler<SimpleTestHandler, TestMessage>();
            });
        });

        var registry = provider.GetRequiredService<HandlerRegistry>();
        var route = new MessageRoute(Topic: nameof(TestMessage), Broker: null);

        // Should be exactly one subscription, not two.
        Assert.That(registry.Handlers[route], Has.Exactly(1).Items);
    }

    [Test]
    public void AddBussy_DoesNotDuplicate_WhenHandlerExplicitlyRegisteredWithCustomRoute()
    {
        using var provider = BuildProvider(sc =>
        {
            sc.AddScoped<SimpleTestHandler>();
            sc.AddBussy(cfg =>
            {
                // Explicit registration for the handler with a custom route.
                cfg.RegisterHandler<SimpleTestHandler, TestMessage>(topic: "custom-topic");
            });
        });

        var registry = provider.GetRequiredService<HandlerRegistry>();

        // The handler was registered explicitly, so auto-discovery should skip it.
        // Only the custom route should be present.
        var customRoute = new MessageRoute(Topic: "custom-topic", Broker: null);
        var defaultRoute = new MessageRoute(Topic: nameof(TestMessage), Broker: null);

        Assert.Multiple(() =>
        {
            Assert.That(registry.Handlers, Does.ContainKey(customRoute));
            Assert.That(registry.Handlers, Does.Not.ContainKey(defaultRoute));
        });
    }

    // ---------------------------------------------------------------------------
    // MessageRouteAttribute honoured during auto-discovery
    // ---------------------------------------------------------------------------

    [Test]
    public void AddBussy_AutoDiscovers_HandlerForAttributedMessage_UsesAttributeRoute()
    {
        using var provider = BuildProvider(sc =>
        {
            sc.AddScoped<TopicOnlyMessageTestHandler>();
            sc.AddBussy();
        });

        var registry = provider.GetRequiredService<HandlerRegistry>();
        var route = new MessageRoute(Topic: "topic-only", Broker: null);

        Assert.That(registry.Handlers, Does.ContainKey(route));
    }

    // ---------------------------------------------------------------------------
    // configure callback still works independently
    // ---------------------------------------------------------------------------

    [Test]
    public void AddBussy_ExplicitConfigure_WorksWithoutDiRegistration()
    {
        using var provider = BuildProvider(sc =>
        {
            // Handler is NOT registered in DI as a service — only declared via configure.
            sc.AddBussy(cfg =>
            {
                cfg.RegisterHandler<SimpleTestHandler, TestMessage>();
            });
        });

        var registry = provider.GetRequiredService<HandlerRegistry>();
        var route = new MessageRoute(Topic: nameof(TestMessage), Broker: null);

        Assert.That(registry.Handlers, Does.ContainKey(route));
    }

    // ---------------------------------------------------------------------------
    // IHostedService is registered
    // ---------------------------------------------------------------------------

    [Test]
    public void AddBussy_RegistersBussyService_AsHostedService()
    {
        using var provider = BuildProvider(sc =>
        {
            sc.AddBussy();
        });

        var hostedServices = provider.GetServices<IHostedService>();
        Assert.That(hostedServices, Has.Exactly(1).InstanceOf<BussyService>());
    }

    // ---------------------------------------------------------------------------
    // AddBussyHandlers + AddBussy integration
    // ---------------------------------------------------------------------------

    [Test]
    public void AddBussyHandlers_CombinedWithAddBussy_AutoSubscribesScannedHandlers()
    {
        using var provider = BuildProvider(sc =>
        {
            sc.AddBussyHandlers(typeof(SimpleTestHandler).Assembly);
            sc.AddBussy();
        });

        var registry = provider.GetRequiredService<HandlerRegistry>();
        var route = new MessageRoute(Topic: nameof(TestMessage), Broker: null);

        Assert.That(registry.Handlers, Does.ContainKey(route));
    }

    // ---------------------------------------------------------------------------
    // Test handler types
    // ---------------------------------------------------------------------------

    private sealed class SimpleTestHandler : IHandler<TestMessage>
    {
        public Task HandleAsync(MessageContext<TestMessage> context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class TopicOnlyMessageTestHandler : IHandler<TopicOnlyMessage>
    {
        public Task HandleAsync(MessageContext<TopicOnlyMessage> context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
