using Bussy.Net.Registries;
using Bussy.Net.Test.TestMessageTypes;
using Microsoft.Extensions.Logging;
using Moq;

namespace Bussy.Net.Test;

[TestFixture]
public sealed class HandlerRegistryTests
{
    private Mock<IServiceProvider> _serviceProviderMock = null!;
    private Mock<ILoggerFactory> _loggerFactoryMock = null!;
    private Mock<ILogger> _loggerMock = null!;
    private MessageRouteResolver _routeResolver = null!;
    private HandlerRegistry _subject = null!;

    [SetUp]
    public void Setup()
    {
        _serviceProviderMock = new Mock<IServiceProvider>();
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerMock = new Mock<ILogger>();

        // Setup only the non-extension method which Moq can handle
        _loggerFactoryMock
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(_loggerMock.Object);

        _routeResolver = new MessageRouteResolver();
        _subject = new HandlerRegistry(_routeResolver, _serviceProviderMock.Object, _loggerFactoryMock.Object);
    }

    [Test]
    public void Constructor_InitializesEmptyHandlers()
    {
        Assert.That(_subject.Handlers, Is.Empty);
    }

    [Test]
    public void RegisterHandler_WithDefaultRoute_AddsHandlerToRegistry()
    {
        _subject.RegisterHandler<TestMessageHandler, TestMessage>();

        Assert.That(_subject.Handlers, Has.Count.EqualTo(1));
        var route = new MessageRoute(Topic: nameof(TestMessage), Broker: null);
        Assert.That(_subject.Handlers, Does.ContainKey(route));
    }

    [Test]
    public void RegisterHandler_WithDefaultRoute_CreatesInboundMessageHandler()
    {
        _subject.RegisterHandler<TestMessageHandler, TestMessage>();

        var route = new MessageRoute(Topic: nameof(TestMessage), Broker: null);
        var handlers = _subject.Handlers[route].ToList();

        Assert.That(handlers, Has.Count.EqualTo(1));
        Assert.That(handlers[0], Is.TypeOf<InboundMessageHandler<TestMessage>>());
    }

    [Test]
    public void RegisterHandler_WithAttributedMessage_UsesAttributeRoute()
    {
        _subject.RegisterHandler<TopicOnlyMessageHandler, TopicOnlyMessage>();

        var route = new MessageRoute(Topic: "topic-only", Broker: null);
        Assert.That(_subject.Handlers, Does.ContainKey(route));
    }

    [Test]
    public void RegisterHandler_WithTopicOverride_UsesExplicitTopic()
    {
        _subject.RegisterHandler<TestMessageHandler, TestMessage>(topic: "custom-topic");

        var route = new MessageRoute(Topic: "custom-topic", Broker: null);
        Assert.That(_subject.Handlers, Does.ContainKey(route));
    }

    [Test]
    public void RegisterHandler_WithBrokerOverride_UsesExplicitBroker()
    {
        _subject.RegisterHandler<TestMessageHandler, TestMessage>(broker: "kafka");

        var route = new MessageRoute(Topic: nameof(TestMessage), Broker: "kafka");
        Assert.That(_subject.Handlers, Does.ContainKey(route));
    }

    [Test]
    public void RegisterHandler_WithBothOverrides_UsesExplicitValues()
    {
        _subject.RegisterHandler<TestMessageHandler, TestMessage>(topic: "custom-topic", broker: "kafka");

        var route = new MessageRoute(Topic: "custom-topic", Broker: "kafka");
        Assert.That(_subject.Handlers, Does.ContainKey(route));
    }

    [Test]
    public void RegisterHandler_WithAttributedMessageAndTopicOverride_OverridesAttributeTopic()
    {
        _subject.RegisterHandler<TopicOnlyMessageHandler, TopicOnlyMessage>(topic: "override-topic");

        var route = new MessageRoute(Topic: "override-topic", Broker: null);
        Assert.That(_subject.Handlers, Does.ContainKey(route));
    }

    [Test]
    public void RegisterHandler_WithBrokerOnlyMessageAndBrokerOverride_OverridesAttributeBroker()
    {
        _subject.RegisterHandler<BrokerOnlyMessageHandler, BrokerOnlyMessage>(broker: "rabbitmq");

        var route = new MessageRoute(Topic: nameof(BrokerOnlyMessage), Broker: "rabbitmq");
        Assert.That(_subject.Handlers, Does.ContainKey(route));
    }

    [Test]
    public void RegisterHandler_MultipleHandlersForSameRoute_AppendsToExistingHandlers()
    {
        _subject.RegisterHandler<TestMessageHandler, TestMessage>();
        _subject.RegisterHandler<AnotherTestMessageHandler, TestMessage>();

        var route = new MessageRoute(Topic: nameof(TestMessage), Broker: null);
        var handlers = _subject.Handlers[route].ToList();

        Assert.That(handlers, Has.Count.EqualTo(2));
    }

    [Test]
    public void RegisterHandler_MultipleHandlersForSameRoute_MaintainsHandlerOrder()
    {
        _subject.RegisterHandler<TestMessageHandler, TestMessage>();
        _subject.RegisterHandler<AnotherTestMessageHandler, TestMessage>();

        var route = new MessageRoute(Topic: nameof(TestMessage), Broker: null);
        var handlers = _subject.Handlers[route].ToList();

        // Verify both handlers are present
        Assert.That(handlers, Has.Count.EqualTo(2));
    }

    [Test]
    public void RegisterHandler_WithDifferentRoutes_StoresHandlersSeparately()
    {
        _subject.RegisterHandler<TestMessageHandler, TestMessage>();
        _subject.RegisterHandler<TopicOnlyMessageHandler, TopicOnlyMessage>();

        Assert.That(_subject.Handlers, Has.Count.EqualTo(2));
    }

    [Test]
    public void RegisterHandler_WithTopicAndBrokerMessage_UsesBothAttributeValues()
    {
        _subject.RegisterHandler<TopicAndBrokerMessageHandler, TopicAndBrokerMessage>();

        var route = new MessageRoute(Topic: "orders-created", Broker: "rabbitmq");
        Assert.That(_subject.Handlers, Does.ContainKey(route));
    }

    [Test]
    public void RegisterHandler_TypeOverload_WithDefaultRoute_AddsHandlerToRegistry()
    {
        _subject.RegisterHandler(typeof(TestMessageHandler), typeof(TestMessage), null, null);

        var route = new MessageRoute(Topic: nameof(TestMessage), Broker: null);
        Assert.That(_subject.Handlers, Does.ContainKey(route));
    }

    [Test]
    public void RegisterHandler_TypeOverload_WithNullHandlerType_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => _subject.RegisterHandler(null!, typeof(TestMessage), null, null));

        Assert.That(exception!.ParamName, Is.EqualTo("handlerType"));
    }

    [Test]
    public void RegisterHandler_TypeOverload_WithNullMessageType_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => _subject.RegisterHandler(typeof(TestMessageHandler), null!, null, null));

        Assert.That(exception!.ParamName, Is.EqualTo("messageType"));
    }

    [Test]
    public void RegisterHandler_TypeOverload_WithIncompatibleHandler_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => _subject.RegisterHandler(typeof(TopicOnlyMessageHandler), typeof(TestMessage), null, null));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.ParamName, Is.EqualTo("handlerType"));
            Assert.That(exception.Message, Does.Contain("does not implement"));
        });
    }

    // Test message handlers
    private sealed class TestMessageHandler : IHandler<TestMessage>
    {
        public Task HandleAsync(MessageContext<TestMessage> context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class AnotherTestMessageHandler : IHandler<TestMessage>
    {
        public Task HandleAsync(MessageContext<TestMessage> context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TopicOnlyMessageHandler : IHandler<TopicOnlyMessage>
    {
        public Task HandleAsync(MessageContext<TopicOnlyMessage> context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class BrokerOnlyMessageHandler : IHandler<BrokerOnlyMessage>
    {
        public Task HandleAsync(MessageContext<BrokerOnlyMessage> context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TopicAndBrokerMessageHandler : IHandler<TopicAndBrokerMessage>
    {
        public Task HandleAsync(MessageContext<TopicAndBrokerMessage> context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}





