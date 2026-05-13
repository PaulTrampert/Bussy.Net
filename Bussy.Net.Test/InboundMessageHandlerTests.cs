using System.Text;
using Bussy.Net.Test.TestMessageTypes;
using Bussy.Net.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Bussy.Net.Test;

[TestFixture]
public sealed class InboundMessageHandlerTests
{
    private Mock<IServiceProvider> _rootServiceProviderMock = null!;
    private Mock<IServiceScopeFactory> _scopeFactoryMock = null!;
    private Mock<IServiceScope> _scopeMock = null!;
    private Mock<IServiceProvider> _scopedServiceProviderMock = null!;
    private Mock<ILogger<InboundMessageHandler<TestMessage>>> _loggerMock = null!;
    private List<LoggedEvent> _logs = null!;

    [SetUp]
    public void Setup()
    {
        _rootServiceProviderMock = new Mock<IServiceProvider>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeMock = new Mock<IServiceScope>();
        _scopedServiceProviderMock = new Mock<IServiceProvider>();
        _loggerMock = new Mock<ILogger<InboundMessageHandler<TestMessage>>>();
        _logs = [];

        _rootServiceProviderMock
            .Setup(p => p.GetService(typeof(IServiceScopeFactory)))
            .Returns(_scopeFactoryMock.Object);

        _scopeFactoryMock
            .Setup(f => f.CreateScope())
            .Returns(_scopeMock.Object);

        _scopeMock
            .SetupGet(s => s.ServiceProvider)
            .Returns(_scopedServiceProviderMock.Object);

        _loggerMock
            .Setup(l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(new InvocationAction(invocation =>
            {
                var level = (LogLevel)invocation.Arguments[0];
                var message = invocation.Arguments[2]?.ToString() ?? string.Empty;
                var exception = invocation.Arguments[3] as Exception;
                _logs.Add(new LoggedEvent(level, message, exception));
            }));
    }

    [Test]
    public void Constructor_HandlerTypeDoesNotImplementIHandler_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new InboundMessageHandler<TestMessage>(_rootServiceProviderMock.Object, typeof(string), _loggerMock.Object));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.ParamName, Is.EqualTo("handlerType"));
            Assert.That(exception.Message, Does.Contain("does not implement"));
            Assert.That(exception.Message, Does.Contain(typeof(IHandler<TestMessage>).FullName!));
        });
    }

    [Test]
    public async Task HandleInboundMessageAsync_HandlerCannotBeResolved_ReturnsDeadLetter()
    {
        var subject = CreateSubject(typeof(TestMessageHandler));
        var message = CreateInboundMessage("{\"Name\":\"alice\",\"Count\":7}");

        _scopedServiceProviderMock
            .Setup(p => p.GetService(typeof(TestMessageHandler)))
            .Returns((object?)null);

        var result = await subject.HandleInboundMessageAsync(message, CancellationToken.None);

        Assert.That(result, Is.EqualTo(AckAction.DeadLetter));
    }

    [Test]
    public async Task HandleInboundMessageAsync_HandlerCannotBeResolved_LogsErrorWithExceptionAndInboundMessage()
    {
        var subject = CreateSubject(typeof(TestMessageHandler));
        var message = CreateInboundMessage("{\"Name\":\"alice\",\"Count\":7}");

        _scopedServiceProviderMock
            .Setup(p => p.GetService(typeof(TestMessageHandler)))
            .Returns((object?)null);

        _ = await subject.HandleInboundMessageAsync(message, CancellationToken.None);

        var error = _logs.Single(e => e.Level == LogLevel.Error);
        Assert.Multiple(() =>
        {
            Assert.That(error.Exception, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Unprocessable message"));
            Assert.That(error.Message, Does.Contain(message.Topic));
            Assert.That(error.Message, Does.Contain(message.Broker));
        });
    }

    [Test]
    public async Task HandleInboundMessageAsync_MessageCannotBeParsed_ReturnsDeadLetter()
    {
        var handlerMock = new Mock<IHandler<TestMessage>>();
        var subject = CreateSubject(typeof(IHandler<TestMessage>));
        var message = CreateInboundMessage("{not-json");

        _scopedServiceProviderMock
            .Setup(p => p.GetService(typeof(IHandler<TestMessage>)))
            .Returns(handlerMock.Object);

        var result = await subject.HandleInboundMessageAsync(message, CancellationToken.None);

        Assert.That(result, Is.EqualTo(AckAction.DeadLetter));
    }

    [Test]
    public async Task HandleInboundMessageAsync_MessageCannotBeParsed_LogsErrorWithExceptionAndInboundMessage()
    {
        var handlerMock = new Mock<IHandler<TestMessage>>();
        var subject = CreateSubject(typeof(IHandler<TestMessage>));
        var message = CreateInboundMessage("{not-json");

        _scopedServiceProviderMock
            .Setup(p => p.GetService(typeof(IHandler<TestMessage>)))
            .Returns(handlerMock.Object);

        _ = await subject.HandleInboundMessageAsync(message, CancellationToken.None);

        var error = _logs.Single(e => e.Level == LogLevel.Error);
        Assert.Multiple(() =>
        {
            Assert.That(error.Exception, Is.TypeOf<System.Text.Json.JsonException>());
            Assert.That(error.Message, Does.Contain("Unprocessable message"));
            Assert.That(error.Message, Does.Contain(message.Topic));
            Assert.That(error.Message, Does.Contain(message.Broker));
        });
    }

    [Test]
    public async Task HandleInboundMessageAsync_MessageDeserializesToNull_ReturnsDeadLetterAndLogsError()
    {
        var handlerMock = new Mock<IHandler<TestMessage>>();
        var subject = CreateSubject(typeof(IHandler<TestMessage>));
        var message = CreateInboundMessage("null");

        _scopedServiceProviderMock
            .Setup(p => p.GetService(typeof(IHandler<TestMessage>)))
            .Returns(handlerMock.Object);

        var result = await subject.HandleInboundMessageAsync(message, CancellationToken.None);

        var error = _logs.Single(e => e.Level == LogLevel.Error);
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(AckAction.DeadLetter));
            Assert.That(error.Exception, Is.TypeOf<MessageNullException>());
            Assert.That(error.Message, Does.Contain("Unprocessable message"));
            Assert.That(error.Message, Does.Contain(message.Topic));
            Assert.That(error.Message, Does.Contain(message.Broker));
        });

        handlerMock.Verify(
            h => h.HandleAsync(It.IsAny<MessageContext<TestMessage>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task HandleInboundMessageAsync_HandlerReceivesCorrectArguments()
    {
        var handlerMock = new Mock<IHandler<TestMessage>>();
        var subject = CreateSubject(typeof(IHandler<TestMessage>));
        var message = CreateInboundMessage("{\"Name\":\"alice\",\"Count\":7}");
        var token = new CancellationTokenSource().Token;

        MessageContext<TestMessage>? capturedContext = null;
        CancellationToken capturedToken = default;

        handlerMock
            .Setup(h => h.HandleAsync(It.IsAny<MessageContext<TestMessage>>(), It.IsAny<CancellationToken>()))
            .Callback<MessageContext<TestMessage>, CancellationToken>((context, cancellationToken) =>
            {
                capturedContext = context;
                capturedToken = cancellationToken;
            })
            .Returns(Task.CompletedTask);

        _scopedServiceProviderMock
            .Setup(p => p.GetService(typeof(IHandler<TestMessage>)))
            .Returns(handlerMock.Object);

        _ = await subject.HandleInboundMessageAsync(message, token);

        Assert.Multiple(() =>
        {
            Assert.That(capturedContext, Is.Not.Null);
            Assert.That(capturedContext!.Id, Is.EqualTo(message.MessageId));
            Assert.That(capturedContext.SentAtUtc, Is.EqualTo(message.SentAtUtc));
            Assert.That(capturedContext.ReceivedAtUtc, Is.EqualTo(message.ReceivedAtUtc));
            Assert.That(capturedContext.Topic, Is.EqualTo(message.Topic));
            Assert.That(capturedContext.Broker, Is.EqualTo(message.Broker));
            Assert.That(capturedContext.DeliveryAttempt, Is.EqualTo(message.DeliveryAttempt));
            Assert.That(capturedContext.Headers, Is.SameAs(message.Headers));
            Assert.That(capturedContext.Message.Name, Is.EqualTo("alice"));
            Assert.That(capturedContext.Message.Count, Is.EqualTo(7));
            Assert.That(capturedToken, Is.EqualTo(token));
        });
    }

    [Test]
    public async Task HandleInboundMessageAsync_HandlerThrowsOperationCanceledException_ReturnsRetryAndLogsWarning()
    {
        var handlerMock = new Mock<IHandler<TestMessage>>();
        var subject = CreateSubject(typeof(IHandler<TestMessage>));
        var message = CreateInboundMessage("{\"Name\":\"alice\",\"Count\":7}");
        var cancellationException = new OperationCanceledException("cancelled");

        handlerMock
            .Setup(h => h.HandleAsync(It.IsAny<MessageContext<TestMessage>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(cancellationException);

        _scopedServiceProviderMock
            .Setup(p => p.GetService(typeof(IHandler<TestMessage>)))
            .Returns(handlerMock.Object);

        var result = await subject.HandleInboundMessageAsync(message, CancellationToken.None);

        var warning = _logs.Single(e => e.Level == LogLevel.Warning);
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(AckAction.Retry));
            Assert.That(warning.Exception, Is.SameAs(cancellationException));
            Assert.That(warning.Message, Does.Contain("Message processing cancelled"));
            Assert.That(warning.Message, Does.Contain(message.Topic));
        });
    }

    [Test]
    public async Task HandleInboundMessageAsync_HandlerThrowsException_ReturnsRetryAndLogsError()
    {
        var handlerMock = new Mock<IHandler<TestMessage>>();
        var subject = CreateSubject(typeof(IHandler<TestMessage>));
        var message = CreateInboundMessage("{\"Name\":\"alice\",\"Count\":7}");
        var expectedException = new InvalidOperationException("boom");

        handlerMock
            .Setup(h => h.HandleAsync(It.IsAny<MessageContext<TestMessage>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        _scopedServiceProviderMock
            .Setup(p => p.GetService(typeof(IHandler<TestMessage>)))
            .Returns(handlerMock.Object);

        var result = await subject.HandleInboundMessageAsync(message, CancellationToken.None);

        var error = _logs.Single(e => e.Level == LogLevel.Error);
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(AckAction.Retry));
            Assert.That(error.Exception, Is.SameAs(expectedException));
            Assert.That(error.Message, Does.Contain("Error processing message"));
            Assert.That(error.Message, Does.Contain(message.Topic));
        });
    }

    [Test]
    public async Task HandleInboundMessageAsync_HandlerCompletesWithoutError_ReturnsAck()
    {
        var handlerMock = new Mock<IHandler<TestMessage>>();
        var subject = CreateSubject(typeof(IHandler<TestMessage>));
        var message = CreateInboundMessage("{\"Name\":\"alice\",\"Count\":7}");

        handlerMock
            .Setup(h => h.HandleAsync(It.IsAny<MessageContext<TestMessage>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _scopedServiceProviderMock
            .Setup(p => p.GetService(typeof(IHandler<TestMessage>)))
            .Returns(handlerMock.Object);

        var result = await subject.HandleInboundMessageAsync(message, CancellationToken.None);

        Assert.That(result, Is.EqualTo(AckAction.Ack));
    }

    [Test]
    public async Task HandleInboundMessageAsync_CustomSerializer_UsesSerializerToDeserialize()
    {
        var expectedMessage = new TestMessage("bob", 42);
        var rawBytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var inboundMessage = new InboundMessage(
            rawBytes,
            "orders.created",
            "rabbitmq",
            new Dictionary<string, string?>(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            1);

        var serializerMock = new Mock<IMessageSerializer>();
        serializerMock
            .Setup(s => s.Deserialize<TestMessage>(It.IsAny<ReadOnlyMemory<byte>>()))
            .Returns(expectedMessage);

        var handlerMock = new Mock<IHandler<TestMessage>>();
        handlerMock
            .Setup(h => h.HandleAsync(It.IsAny<MessageContext<TestMessage>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _scopedServiceProviderMock
            .Setup(p => p.GetService(typeof(IHandler<TestMessage>)))
            .Returns(handlerMock.Object);

        var subject = new InboundMessageHandler<TestMessage>(
            _rootServiceProviderMock.Object,
            typeof(IHandler<TestMessage>),
            _loggerMock.Object,
            serializerMock.Object);

        var result = await subject.HandleInboundMessageAsync(inboundMessage, CancellationToken.None);

        Assert.That(result, Is.EqualTo(AckAction.Ack));
        serializerMock.Verify(s => s.Deserialize<TestMessage>(It.Is<ReadOnlyMemory<byte>>(b => b.ToArray().SequenceEqual(rawBytes))), Times.Once);
        handlerMock.Verify(h => h.HandleAsync(
            It.Is<MessageContext<TestMessage>>(ctx => ctx.Message == expectedMessage),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private InboundMessageHandler<TestMessage> CreateSubject(Type handlerType)
    {
        return new InboundMessageHandler<TestMessage>(_rootServiceProviderMock.Object, handlerType, _loggerMock.Object);
    }

    private static InboundMessage CreateInboundMessage(string body)
    {
        var sentAt = new DateTimeOffset(2026, 3, 22, 10, 0, 0, TimeSpan.Zero);
        var receivedAt = sentAt.AddSeconds(2);
        return new InboundMessage(
            Encoding.UTF8.GetBytes(body),
            "orders.created",
            "rabbitmq",
            new Dictionary<string, string?>
            {
                ["trace-id"] = "abc123"
            },
            Guid.NewGuid(),
            sentAt,
            receivedAt,
            3);
    }

    private sealed record LoggedEvent(LogLevel Level, string Message, Exception? Exception);


    private sealed class TestMessageHandler : IHandler<TestMessage>
    {
        public Task HandleAsync(MessageContext<TestMessage> context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}



