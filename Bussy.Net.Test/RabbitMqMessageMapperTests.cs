using System.Text;
using Bussy.Net.Transport;
using Bussy.Net.Transports.RabbitMQ;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Bussy.Net.Test;

[TestFixture]
public sealed class RabbitMqMessageMapperTests
{
    private RabbitMqMessageMapper _subject = null!;

    [SetUp]
    public void Setup()
    {
        _subject = new RabbitMqMessageMapper();
    }

    [Test]
    public void MapOutbound_MapsHeadersAndProperties()
    {
        var sentAt = DateTimeOffset.UtcNow;
        var messageId = Guid.NewGuid();
        var message = new OutboundMessage(
            Body: Encoding.UTF8.GetBytes("payload"),
            Topic: "orders.created",
            Broker: "broker-a",
            Headers: new Dictionary<string, string?>
            {
                ["trace-id"] = "abc-123"
            },
            MessageId: messageId,
            SentAtUtc: sentAt,
            PartitionKey: "customer-42");

        var properties = _subject.MapOutbound(message);

        Assert.That(properties.MessageId, Is.EqualTo(messageId.ToString("D")));
        Assert.That(properties.Timestamp.UnixTime, Is.EqualTo(sentAt.ToUnixTimeSeconds()));
        Assert.That(properties.DeliveryMode, Is.EqualTo(DeliveryModes.Persistent));
        Assert.That(properties.Headers!["trace-id"], Is.EqualTo("abc-123"));
        Assert.That(properties.Headers!["bussy.topic"], Is.EqualTo("orders.created"));
        Assert.That(properties.Headers!["bussy.broker"], Is.EqualTo("broker-a"));
        Assert.That(properties.Headers!["bussy.sent-at-utc"], Is.EqualTo(sentAt.ToString("O")));
        Assert.That(properties.Headers!["bussy.partition-key"], Is.EqualTo("customer-42"));
    }

    [Test]
    public void Map_ReadsInboundFieldsFromRabbitMqPayloadAndHeaders()
    {
        var sentAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var messageId = Guid.NewGuid();
        var body = Encoding.UTF8.GetBytes("hello");

        var delivery = CreateDelivery(
            exchange: "orders.exchange",
            redelivered: false,
            messageId: messageId.ToString("D"),
            timestamp: new AmqpTimestamp(sentAt.ToUnixTimeSeconds()),
            headers: new Dictionary<string, object?>
            {
                ["bussy.topic"] = "orders.created",
                ["bussy.broker"] = "broker-a",
                ["bussy.sent-at-utc"] = sentAt.ToString("O"),
                ["bussy.partition-key"] = "customer-42",
                ["trace-id"] = "abc-123"
            },
            body: body);

        var before = DateTimeOffset.UtcNow;
        var mapped = _subject.MapInbound(delivery, "rabbitmq");
        var after = DateTimeOffset.UtcNow;

        Assert.That(mapped.Body.ToArray(), Is.EqualTo(body));
        Assert.That(mapped.Topic, Is.EqualTo("orders.created"));
        Assert.That(mapped.Broker, Is.EqualTo("broker-a"));
        Assert.That(mapped.MessageId, Is.EqualTo(messageId));
        Assert.That(mapped.SentAtUtc, Is.EqualTo(sentAt));
        Assert.That(mapped.ReceivedAtUtc, Is.InRange(before, after));
        Assert.That(mapped.DeliveryAttempt, Is.EqualTo(1));
        Assert.That(mapped.Headers["trace-id"], Is.EqualTo("abc-123"));
        Assert.That(mapped.Headers["bussy.partition-key"], Is.EqualTo("customer-42"));
    }

    [Test]
    public void Map_UsesFallbacksWhenTransportHeadersAreMissing()
    {
        var sentAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        var delivery = CreateDelivery(
            exchange: "events.fallback",
            redelivered: true,
            messageId: Guid.NewGuid().ToString("D"),
            timestamp: new AmqpTimestamp(sentAt.ToUnixTimeSeconds()),
            headers: new Dictionary<string, object?>
            {
                ["trace-id"] = "fallback-trace"
            },
            body: [1, 2, 3]);

        var mapped = _subject.MapInbound(delivery, "transport-name");

        Assert.That(mapped.Topic, Is.EqualTo("events.fallback"));
        Assert.That(mapped.Broker, Is.EqualTo("transport-name"));
        Assert.That(mapped.SentAtUtc.ToUnixTimeSeconds(), Is.EqualTo(sentAt.ToUnixTimeSeconds()));
        Assert.That(mapped.DeliveryAttempt, Is.EqualTo(2));
    }

    [Test]
    public void Map_DeliveryAttempt_UsesDeliveryCountHeaderPrecedence()
    {
        var delivery = CreateDelivery(
            exchange: "events",
            redelivered: false,
            messageId: Guid.NewGuid().ToString("D"),
            timestamp: new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            headers: new Dictionary<string, object?>
            {
                ["x-delivery-count"] = 4,
                ["x-death"] = new List<object?>
                {
                    new Dictionary<string, object?> { ["count"] = 100 }
                }
            },
            body: [9]);

        var mapped = _subject.MapInbound(delivery, "rabbitmq");

        Assert.That(mapped.DeliveryAttempt, Is.EqualTo(5));
    }

    [Test]
    public void Map_DeliveryAttempt_UsesDeathCountWhenDeliveryCountMissing()
    {
        var delivery = CreateDelivery(
            exchange: "events",
            redelivered: false,
            messageId: Guid.NewGuid().ToString("D"),
            timestamp: new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            headers: new Dictionary<string, object?>
            {
                ["x-death"] = new List<object?>
                {
                    new Dictionary<string, object?> { ["count"] = 2 },
                    new Dictionary<string, object?> { ["count"] = 3 }
                }
            },
            body: [9]);

        var mapped = _subject.MapInbound(delivery, "rabbitmq");

        Assert.That(mapped.DeliveryAttempt, Is.EqualTo(6));
    }

    private static BasicDeliverEventArgs CreateDelivery(
        string exchange,
        bool redelivered,
        string messageId,
        AmqpTimestamp timestamp,
        IDictionary<string, object?> headers,
        byte[] body)
    {
        var properties = new BasicProperties
        {
            MessageId = messageId,
            Timestamp = timestamp,
            Headers = headers
        };

        return new BasicDeliverEventArgs(
            "ctag",
            1,
            redelivered,
            exchange,
            string.Empty,
            properties,
            body,
            CancellationToken.None);
    }
}







