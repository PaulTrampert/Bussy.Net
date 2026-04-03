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

        AckHandler.Reset();
        ContextCapturingHandler.Reset();
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
    public void Constructor_HandlerTypeIsNotConcrete_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new InboundMessageHandler<TestMessage>(_rootServiceProviderMock.Object, typeof(IHandler<TestMessage>), _loggerMock.Object));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.ParamName, Is.EqualTo("handlerType"));
            Assert.That(exception.Message, Does.Contain("must be a concrete type"));
        });
    }

    [Test]
    public async Task HandleInboundMessageAsync_HandlerCannotBeResolved_ReturnsDeadLetter()
    {
        var subject = CreateSubject(typeof(HandlerWithRequiredDependency));
        var message = CreateInboundMessage("{\"Name\":\"alice\",\"Count\":7}");

        var result = await subject.HandleInboundMessageAsync(message, CancellationToken.None);

        Assert.That(result, Is.EqualTo(AckAction.DeadLetter));
    }

    [Test]
    public async Task HandleInboundMessageAsync_HandlerCannotBeResolved_LogsErrorWithExceptionAndInboundMessage()
    {
        var subject = CreateSubject(typeof(HandlerWithRequiredDependency));
        var message = CreateInboundMessage("{\"Name\":\"alice\",\"Count\":7}");

        _ = await subject.HandleInboundMessageAsync(message, CancellationToken.None);

        var error = _logs.Single(e => e.Level == LogLevel.Error);
        Assert.Multiple(() =>
        {
            Assert.That(error.Exception, Is.TypeOf<InvalidOperationException>());
            Assert.That(error.Message, Does.Contain("Unprocessable message"));
            Assert.That(error.Message, Does.Contain(message.Topic));
            Assert.That(error.Message, Does.Contain(message.Broker));
        });
    }

    [Test]
    public async Task HandleInboundMessageAsync_MessageCannotBeParsed_ReturnsDeadLetter()
    {
        var subject = CreateSubject(typeof(AckHandler));
        var message = CreateInboundMessage("{not-json");

        var result = await subject.HandleInboundMessageAsync(message, CancellationToken.None);

        Assert.That(result, Is.EqualTo(AckAction.DeadLetter));
    }

    [Test]
    public async Task HandleInboundMessageAsync_MessageCannotBeParsed_LogsErrorWithExceptionAndInboundMessage()
    {
        var subject = CreateSubject(typeof(AckHandler));
        var message = CreateInboundMessage("{not-json");

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
        var subject = CreateSubject(typeof(AckHandler));
        var message = CreateInboundMessage("null");

        var result = await subject.HandleInboundMessageAsync(message, CancellationToken.None);

        var error = _logs.Single(e => e.Level == LogLevel.Error);
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(AckAction.DeadLetter));
            Assert.That(error.Exception, Is.TypeOf<MessageNullException>());
            Assert.That(error.Message, Does.Contain("Unprocessable message"));
            Assert.That(error.Message, Does.Contain(message.Topic));
            Assert.That(error.Message, Does.Contain(message.Broker));
            Assert.That(AckHandler.CallCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task HandleInboundMessageAsync_HandlerReceivesCorrectArguments()
    {
        var subject = CreateSubject(typeof(ContextCapturingHandler));
        var message = CreateInboundMessage("{\"Name\":\"alice\",\"Count\":7}");
        var token = new CancellationTokenSource().Token;

        _ = await subject.HandleInboundMessageAsync(message, token);

        var capturedContext = ContextCapturingHandler.LastContext;
        var capturedToken = ContextCapturingHandler.LastToken;

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
        var subject = CreateSubject(typeof(OperationCanceledHandler));
        var message = CreateInboundMessage("{\"Name\":\"alice\",\"Count\":7}");

        var result = await subject.HandleInboundMessageAsync(message, CancellationToken.None);

        var warning = _logs.Single(e => e.Level == LogLevel.Warning);
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(AckAction.Retry));
            Assert.That(warning.Exception, Is.TypeOf<OperationCanceledException>());
            Assert.That(warning.Exception!.Message, Is.EqualTo("cancelled"));
            Assert.That(warning.Message, Does.Contain("Message processing cancelled"));
            Assert.That(warning.Message, Does.Contain(message.Topic));
        });
    }

    [Test]
    public async Task HandleInboundMessageAsync_HandlerThrowsException_ReturnsRetryAndLogsError()
    {
        var subject = CreateSubject(typeof(ThrowingHandler));
        var message = CreateInboundMessage("{\"Name\":\"alice\",\"Count\":7}");

        var result = await subject.HandleInboundMessageAsync(message, CancellationToken.None);

        var error = _logs.Single(e => e.Level == LogLevel.Error);
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(AckAction.Retry));
            Assert.That(error.Exception, Is.TypeOf<InvalidOperationException>());
            Assert.That(error.Exception!.Message, Is.EqualTo("boom"));
            Assert.That(error.Message, Does.Contain("Error processing message"));
            Assert.That(error.Message, Does.Contain(message.Topic));
        });
    }

    [Test]
    public async Task HandleInboundMessageAsync_HandlerCompletesWithoutError_ReturnsAck()
    {
        var subject = CreateSubject(typeof(AckHandler));
        var message = CreateInboundMessage("{\"Name\":\"alice\",\"Count\":7}");

        var result = await subject.HandleInboundMessageAsync(message, CancellationToken.None);

        Assert.That(result, Is.EqualTo(AckAction.Ack));
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


    private sealed class AckHandler : IHandler<TestMessage>
    {
        public static int CallCount { get; private set; }

        public static void Reset() => CallCount = 0;

        public Task HandleAsync(MessageContext<TestMessage> context, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class ContextCapturingHandler : IHandler<TestMessage>
    {
        public static MessageContext<TestMessage>? LastContext { get; private set; }
        public static CancellationToken LastToken { get; private set; }

        public static void Reset()
        {
            LastContext = null;
            LastToken = default;
        }

        public Task HandleAsync(MessageContext<TestMessage> context, CancellationToken cancellationToken = default)
        {
            LastContext = context;
            LastToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class OperationCanceledHandler : IHandler<TestMessage>
    {
        public Task HandleAsync(MessageContext<TestMessage> context, CancellationToken cancellationToken = default)
        {
            throw new OperationCanceledException("cancelled");
        }
    }

    private sealed class ThrowingHandler : IHandler<TestMessage>
    {
        public Task HandleAsync(MessageContext<TestMessage> context, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("boom");
        }
    }

    private interface IMissingDependency;

    private sealed class HandlerWithRequiredDependency(IMissingDependency missingDependency) : IHandler<TestMessage>
    {
        private readonly IMissingDependency _missingDependency = missingDependency;

        public Task HandleAsync(MessageContext<TestMessage> context, CancellationToken cancellationToken = default)
        {
            _ = _missingDependency;
            return Task.CompletedTask;
        }
    }
}



