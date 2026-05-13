using System.Globalization;
using System.Text;
using Bussy.Net.Transport;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Bussy.Net.Transports.RabbitMq;

public sealed class RabbitMqMessageMapper : IRabbitMqMessageMapper
{
    private const string TopicHeader = "bussy.topic";
    private const string BrokerHeader = "bussy.broker";
    private const string SentAtUtcHeader = "bussy.sent-at-utc";
    private const string DeliveryCountHeader = "x-delivery-count";
    private const string DeathHeader = "x-death";

    public BasicProperties MapOutbound(OutboundMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var headers = new Dictionary<string, object?>();
        foreach (var (key, value) in message.Headers)
        {
            headers[key] = value;
        }

        headers[TopicHeader] = message.Topic;
        headers[BrokerHeader] = message.Broker;
        headers[SentAtUtcHeader] = message.SentAtUtc.ToString("O");

        return new BasicProperties
        {
            MessageId = message.MessageId.ToString("D"),
            Timestamp = new AmqpTimestamp(message.SentAtUtc.ToUnixTimeSeconds()),
            DeliveryMode = DeliveryModes.Persistent,
            Headers = headers
        };
    }

    public InboundMessage MapInbound(BasicDeliverEventArgs eventArgs, string transportName)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        ArgumentException.ThrowIfNullOrWhiteSpace(transportName);

        var headers = ExtractApplicationHeaders(eventArgs.BasicProperties.Headers);

        var topic = GetHeaderString(eventArgs.BasicProperties.Headers, TopicHeader) ?? eventArgs.Exchange;
        var broker = GetHeaderString(eventArgs.BasicProperties.Headers, BrokerHeader) ?? transportName;

        var messageIdRaw = eventArgs.BasicProperties.MessageId;
        _ = Guid.TryParse(messageIdRaw, out var messageId);

        var sentAtUtcRaw = GetHeaderString(eventArgs.BasicProperties.Headers, SentAtUtcHeader);
        var sentAtUtc = TryParseDateTimeOffset(sentAtUtcRaw)
                        ?? TryGetTimestamp(eventArgs.BasicProperties.Timestamp)
                        ?? DateTimeOffset.UtcNow;

        var deliveryAttempt = ResolveDeliveryAttempt(eventArgs);

        return new InboundMessage(
            eventArgs.Body,
            topic,
            broker,
            headers,
            messageId,
            sentAtUtc,
            DateTimeOffset.UtcNow,
            deliveryAttempt);
    }

    private static IReadOnlyDictionary<string, string?> ExtractApplicationHeaders(IDictionary<string, object?>? headers)
    {
        var result = new Dictionary<string, string?>();
        if (headers is null)
        {
            return result;
        }

        foreach (var (key, value) in headers)
        {
            if (key is TopicHeader or BrokerHeader or SentAtUtcHeader)
            {
                continue;
            }

            result[key] = ConvertHeaderValueToString(value);
        }

        return result;
    }

    private static int ResolveDeliveryAttempt(BasicDeliverEventArgs eventArgs)
    {
        if (TryGetHeaderInt(eventArgs.BasicProperties.Headers, DeliveryCountHeader, out var deliveryCount))
        {
            return Math.Max(1, deliveryCount + 1);
        }

        if (TryGetDeathCount(eventArgs.BasicProperties.Headers, out var deathCount))
        {
            return Math.Max(1, deathCount + 1);
        }

        return eventArgs.Redelivered ? 2 : 1;
    }

    private static bool TryGetDeathCount(IDictionary<string, object?>? headers, out int deathCount)
    {
        deathCount = 0;
        if (headers is null || !headers.TryGetValue(DeathHeader, out var deathHeader) || deathHeader is null)
        {
            return false;
        }

        if (deathHeader is IList<object?> entries)
        {
            var total = 0;
            foreach (var entry in entries)
            {
                if (entry is IDictionary<string, object?> deathEntry &&
                    deathEntry.TryGetValue("count", out var value) &&
                    value is not null &&
                    TryConvertToInt(value, out var count))
                {
                    total += count;
                }
            }

            if (total > 0)
            {
                deathCount = total;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetHeaderInt(IDictionary<string, object?>? headers, string key, out int value)
    {
        value = 0;
        if (headers is null || !headers.TryGetValue(key, out var rawValue) || rawValue is null)
        {
            return false;
        }

        return TryConvertToInt(rawValue, out value);
    }

    private static bool TryConvertToInt(object rawValue, out int value)
    {
        switch (rawValue)
        {
            case byte b:
                value = b;
                return true;
            case sbyte sb:
                value = sb;
                return true;
            case short s:
                value = s;
                return true;
            case ushort us:
                value = us;
                return true;
            case int i:
                value = i;
                return true;
            case uint ui when ui <= int.MaxValue:
                value = (int)ui;
                return true;
            case long l when l is >= int.MinValue and <= int.MaxValue:
                value = (int)l;
                return true;
            case ulong ul when ul <= int.MaxValue:
                value = (int)ul;
                return true;
            case byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var parsed):
                value = parsed;
                return true;
            default:
                value = 0;
                return false;
        }
    }

    private static string? GetHeaderString(IDictionary<string, object?>? headers, string key)
    {
        if (headers is null || !headers.TryGetValue(key, out var value))
        {
            return null;
        }

        return ConvertHeaderValueToString(value);
    }

    private static string? ConvertHeaderValueToString(object? value)
    {
        return value switch
        {
            null => null,
            string s => s,
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            ReadOnlyMemory<byte> memory => Encoding.UTF8.GetString(memory.Span),
            AmqpTimestamp timestamp => timestamp.UnixTime.ToString(CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };
    }

    private static DateTimeOffset? TryParseDateTimeOffset(string? raw)
    {
        if (raw is null)
        {
            return null;
        }

        return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
    }

    private static DateTimeOffset? TryGetTimestamp(AmqpTimestamp timestamp)
    {
        return timestamp.UnixTime <= 0
            ? null
            : DateTimeOffset.FromUnixTimeSeconds(timestamp.UnixTime);
    }
}


