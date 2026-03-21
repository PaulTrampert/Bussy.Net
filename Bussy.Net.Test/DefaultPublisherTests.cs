using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bussy.Net.Transport;
using Moq;

namespace Bussy.Net.Test;

[TestFixture]
public sealed class DefaultPublisherTests
{
    private Mock<ITransport> _sqsMock = null!;
    private Mock<ITransport> _rabbitMock = null!;
    private DefaultPublisher _subject = null!;

    [SetUp]
    public void Setup()
    {
        _sqsMock = new Mock<ITransport>();
        _sqsMock.Setup(t => t.Name).Returns("sqs");
        _sqsMock.Setup(t => t.SendAsync(It.IsAny<OutboundMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _sqsMock.Setup(t => t.SendBatchAsync(It.IsAny<IReadOnlyCollection<OutboundMessage>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _rabbitMock = new Mock<ITransport>();
        _rabbitMock.Setup(t => t.Name).Returns("rabbitmq");
        _rabbitMock.Setup(t => t.SendAsync(It.IsAny<OutboundMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _rabbitMock.Setup(t => t.SendBatchAsync(It.IsAny<IReadOnlyCollection<OutboundMessage>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _subject = new DefaultPublisher([_sqsMock.Object, _rabbitMock.Object]);
    }

    // --- Constructor ---

    [Test]
    public void Constructor_NullTransports_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DefaultPublisher(null!));
    }

    [Test]
    public void Constructor_EmptyTransports_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new DefaultPublisher([]));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Constructor_TransportWithBlankName_ThrowsArgumentException(string blankName)
    {
        var badTransport = new Mock<ITransport>();
        badTransport.Setup(t => t.Name).Returns(blankName);

        Assert.Throws<ArgumentException>(() => new DefaultPublisher([badTransport.Object]));
    }

    [Test]
    public void Constructor_DuplicateTransportNames_ThrowsArgumentException()
    {
        var duplicate = new Mock<ITransport>();
        duplicate.Setup(t => t.Name).Returns("sqs");

        Assert.Throws<ArgumentException>(() => new DefaultPublisher([_sqsMock.Object, duplicate.Object]));
    }

    // --- Single message, no overrides ---

    [Test]
    public async Task PublishAsync_SingleMessage_SendsToAllTransports()
    {
        await _subject.PublishAsync(new PlainMessage());

        _sqsMock.Verify(t => t.SendAsync(It.IsAny<OutboundMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        _rabbitMock.Verify(t => t.SendAsync(It.IsAny<OutboundMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task PublishAsync_SingleMessage_UsesClassNameAsTopic()
    {
        await _subject.PublishAsync(new PlainMessage());

        _sqsMock.Verify(t => t.SendAsync(
            It.Is<OutboundMessage>(m => m.Topic == nameof(PlainMessage)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task PublishAsync_SingleMessage_WithAttributeTopic_UsesAttributeTopic()
    {
        await _subject.PublishAsync(new MessageWithTopic());

        _sqsMock.Verify(t => t.SendAsync(
            It.Is<OutboundMessage>(m => m.Topic == "custom-topic"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task PublishAsync_SingleMessage_WithAttributeBroker_SendsOnlyToMatchingTransport()
    {
        await _subject.PublishAsync(new MessageWithBroker());

        _sqsMock.Verify(t => t.SendAsync(It.IsAny<OutboundMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        _rabbitMock.Verify(t => t.SendAsync(It.IsAny<OutboundMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- Batch messages, no overrides ---

    [Test]
    public async Task PublishAsync_EmptyMessageCollection_DoesNotSend()
    {
        await _subject.PublishManyAsync(Array.Empty<PlainMessage>());

        _sqsMock.Verify(t => t.SendAsync(It.IsAny<OutboundMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        _sqsMock.Verify(t => t.SendBatchAsync(It.IsAny<IReadOnlyCollection<OutboundMessage>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task PublishAsync_SingleItemCollection_CallsSendAsync()
    {
        await _subject.PublishManyAsync(new[] { new PlainMessage() });

        _sqsMock.Verify(t => t.SendAsync(It.IsAny<OutboundMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        _sqsMock.Verify(t => t.SendBatchAsync(It.IsAny<IReadOnlyCollection<OutboundMessage>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task PublishAsync_MultipleItemCollection_CallsSendBatchAsyncOnAllTransports()
    {
        var messages = new[] { new PlainMessage(), new PlainMessage(), new PlainMessage() };

        await _subject.PublishManyAsync(messages);

        _sqsMock.Verify(t => t.SendBatchAsync(
            It.Is<IReadOnlyCollection<OutboundMessage>>(b => b.Count == 3),
            It.IsAny<CancellationToken>()), Times.Once);
        _rabbitMock.Verify(t => t.SendBatchAsync(
            It.Is<IReadOnlyCollection<OutboundMessage>>(b => b.Count == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Topic override ---

    [Test]
    public async Task PublishAsync_WithTopicOverride_UsesExplicitTopic()
    {
        await _subject.PublishAsync(new PlainMessage(), "override-topic");

        _sqsMock.Verify(t => t.SendAsync(
            It.Is<OutboundMessage>(m => m.Topic == "override-topic"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase("")]
    [TestCase("   ")]
    public void PublishAsync_WithInvalidTopicOverride_ThrowsArgumentException(string invalidTopic)
    {
        Assert.ThrowsAsync<ArgumentException>(() => _subject.PublishAsync(new PlainMessage(), invalidTopic));
    }

    // --- Broker override ---

    [Test]
    public async Task PublishAsync_WithBrokerOverride_SendsOnlyToMatchingTransport()
    {
        await _subject.PublishAsync(new PlainMessage(), "any-topic", "sqs");

        _sqsMock.Verify(t => t.SendAsync(It.IsAny<OutboundMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        _rabbitMock.Verify(t => t.SendAsync(It.IsAny<OutboundMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void PublishAsync_WithUnknownBroker_ThrowsArgumentException()
    {
        Assert.ThrowsAsync<ArgumentException>(() => _subject.PublishAsync(new PlainMessage(), "any-topic", "unknown"));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void PublishAsync_WithInvalidBrokerOverride_ThrowsArgumentException(string invalidBroker)
    {
        Assert.ThrowsAsync<ArgumentException>(() => _subject.PublishAsync(new PlainMessage(), "any-topic", invalidBroker));
    }

    // --- Batch variants with overrides ---

    [Test]
    public async Task PublishAsync_BatchWithTopicOverride_UsesExplicitTopic()
    {
        var messages = new[] { new PlainMessage(), new PlainMessage() };

        await _subject.PublishManyAsync(messages, "override-topic");

        _sqsMock.Verify(t => t.SendBatchAsync(
            It.Is<IReadOnlyCollection<OutboundMessage>>(b => b.All(m => m.Topic == "override-topic")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task PublishAsync_BatchWithBrokerOverride_SendsOnlyToMatchingTransport()
    {
        var messages = new[] { new PlainMessage(), new PlainMessage() };

        await _subject.PublishManyAsync(messages, "any-topic", "sqs");

        _sqsMock.Verify(t => t.SendBatchAsync(It.IsAny<IReadOnlyCollection<OutboundMessage>>(), It.IsAny<CancellationToken>()), Times.Once);
        _rabbitMock.Verify(t => t.SendBatchAsync(It.IsAny<IReadOnlyCollection<OutboundMessage>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- Test message types ---

    private sealed class PlainMessage;

    [Message("custom-topic")]
    private sealed class MessageWithTopic;

    [Message(Broker = "sqs")]
    private sealed class MessageWithBroker;
}