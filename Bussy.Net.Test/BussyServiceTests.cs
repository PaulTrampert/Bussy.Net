using Bussy.Net.Registries;
using Bussy.Net.Test.TestMessageTypes;
using Bussy.Net.Transport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Bussy.Net.Test;

[TestFixture]
public sealed class BussyServiceTests
{
    private HandlerRegistry _handlerRegistry = null!;
    private TransportRegistry _transportRegistry = null!;
    private ILogger<BussyService> _logger = null!;
    private BussyService _subject = null!;

    [SetUp]
    public void SetUp()
    {
        _handlerRegistry = CreateHandlerRegistry();
        _transportRegistry = new TransportRegistry();
        _logger = NullLogger<BussyService>.Instance;
        _subject = new BussyService(_handlerRegistry, _transportRegistry, _logger);
    }

    [TearDown]
    public void TearDown()
    {
        _subject.Dispose();
    }

    [Test]
    public async Task StartAsync_SubscribesAllHandlersWithMessageRouteTopics()
    {
        _handlerRegistry.RegisterHandler<HandlerA, TestMessage>(topic: "topic-a");
        _handlerRegistry.RegisterHandler<HandlerB, TestMessage>(topic: "topic-a");
        _handlerRegistry.RegisterHandler<HandlerC, TestMessage>(topic: "topic-b", broker: "rabbitmq");

        var rabbitTopics = new List<string>();
        var kafkaTopics = new List<string>();

        var rabbit = CreateTransport("rabbitmq", rabbitTopics);
        var kafka = CreateTransport("kafka", kafkaTopics);
        _transportRegistry.Register(rabbit.Object);
        _transportRegistry.Register(kafka.Object);

        await _subject.StartAsync(CancellationToken.None);
        await Task.Delay(50);
        await _subject.StopAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(rabbitTopics.Count(t => t == "topic-a"), Is.EqualTo(2));
            Assert.That(rabbitTopics.Count(t => t == "topic-b"), Is.EqualTo(1));
            Assert.That(kafkaTopics.Count(t => t == "topic-a"), Is.EqualTo(2));
            Assert.That(kafkaTopics.Count(t => t == "topic-b"), Is.EqualTo(0));
        });
    }

    [Test]
    public async Task StopAsync_DisposesAllSubscriptions()
    {
        _handlerRegistry.RegisterHandler<HandlerA, TestMessage>(topic: "topic-a");
        _handlerRegistry.RegisterHandler<HandlerB, TestMessage>(topic: "topic-a");

        var subscriptions = new List<Mock<ITransportSubscription>>();
        var rabbit = CreateTransport("rabbitmq", null, subscriptions);
        var kafka = CreateTransport("kafka", null, subscriptions);
        _transportRegistry.Register(rabbit.Object);
        _transportRegistry.Register(kafka.Object);

        await _subject.StartAsync(CancellationToken.None);
        await Task.Delay(50);
        await _subject.StopAsync(CancellationToken.None);

        Assert.That(subscriptions, Has.Count.EqualTo(4));
        foreach (var subscription in subscriptions)
        {
            subscription.Verify(s => s.DisposeAsync(), Times.Once);
        }
    }

    [Test]
    public async Task StopAsync_CancelsExecuteStoppingToken()
    {
        _handlerRegistry.RegisterHandler<HandlerA, TestMessage>(topic: "topic-a");

        var tokenCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var rabbit = CreateTransport(
            "rabbitmq",
            null,
            null,
            token => token.Register(() => tokenCancelled.TrySetResult()));
        _transportRegistry.Register(rabbit.Object);

        await _subject.StartAsync(CancellationToken.None);
        await Task.Delay(50);
        await _subject.StopAsync(CancellationToken.None);

        await tokenCancelled.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(_subject.ExecuteTask, Is.Not.Null);
        Assert.That(_subject.ExecuteTask!.IsCompleted, Is.True);
    }

    [Test]
    public async Task ExecuteTask_DoesNotCompleteUntilServiceIsStopped()
    {
        await _subject.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        Assert.That(_subject.ExecuteTask, Is.Not.Null);
        Assert.That(_subject.ExecuteTask!.IsCompleted, Is.False);

        await _subject.StopAsync(CancellationToken.None);

        await _subject.ExecuteTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(_subject.ExecuteTask.IsCompleted, Is.True);
    }

    private static HandlerRegistry CreateHandlerRegistry()
    {
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(Mock.Of<ILogger>());

        return new HandlerRegistry(new MessageRouteResolver(), Mock.Of<IServiceProvider>(), loggerFactory.Object);
    }

    private static Mock<ITransport> CreateTransport(
        string name,
        List<string>? topics,
        List<Mock<ITransportSubscription>>? createdSubscriptions = null,
        Action<CancellationToken>? onToken = null)
    {
        var transport = new Mock<ITransport>();
        transport.Setup(t => t.Name).Returns(name);
        transport.Setup(t => t.Capabilities).Returns(TransportCapability.None);
        transport
            .Setup(t => t.SubscribeAsync(
                It.IsAny<string>(),
                It.IsAny<Func<InboundMessage, CancellationToken, Task<AckAction>>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Func<InboundMessage, CancellationToken, Task<AckAction>>, CancellationToken>((topic, _, token) =>
            {
                topics?.Add(topic);
                onToken?.Invoke(token);
            })
            .Returns(() =>
            {
                var subscription = new Mock<ITransportSubscription>();
                subscription.Setup(s => s.DisposeAsync()).Returns(ValueTask.CompletedTask);
                createdSubscriptions?.Add(subscription);
                return Task.FromResult(subscription.Object);
            });

        return transport;
    }

    private sealed class HandlerA : IHandler<TestMessage>
    {
        public Task HandleAsync(MessageContext<TestMessage> context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class HandlerB : IHandler<TestMessage>
    {
        public Task HandleAsync(MessageContext<TestMessage> context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class HandlerC : IHandler<TestMessage>
    {
        public Task HandleAsync(MessageContext<TestMessage> context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}




