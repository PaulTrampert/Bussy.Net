using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Bussy.Net.Transport;

namespace Bussy.Net;

internal sealed class DefaultPublisher : IPublisher
{
    private static readonly IReadOnlyDictionary<string, string?> EmptyHeaders = new Dictionary<string, string?>();

    private readonly MessageRouteResolver _routeResolver;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly IReadOnlyList<ITransport> _transports;
    private readonly IReadOnlyDictionary<string, ITransport> _transportsByName;

    public DefaultPublisher(
        IEnumerable<ITransport> transports,
        MessageRouteResolver? routeResolver = null,
        JsonSerializerOptions? serializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(transports);

        _transports = transports.ToArray();
        if (_transports.Count == 0)
        {
            throw new ArgumentException("At least one transport must be registered.", nameof(transports));
        }

        _transportsByName = BuildTransportLookup(_transports);
        _routeResolver = routeResolver ?? new MessageRouteResolver();
        _serializerOptions = serializerOptions ?? new JsonSerializerOptions();
    }

    public Task PublishAsync<T>(T message)
    {
        return PublishCoreAsync(Single(message), topicOverride: null, brokerOverride: null);
    }

    public Task PublishManyAsync<T>(IEnumerable<T> messages)
    {
        return PublishCoreAsync(messages, topicOverride: null, brokerOverride: null);
    }

    public Task PublishAsync<T>(T message, string topic)
    {
        return PublishCoreAsync(Single(message), topicOverride: topic, brokerOverride: null);
    }

    public Task PublishManyAsync<T>(IEnumerable<T> messages, string topic)
    {
        return PublishCoreAsync(messages, topicOverride: topic, brokerOverride: null);
    }

    public Task PublishAsync<T>(T message, string topic, string broker)
    {
        return PublishCoreAsync(Single(message), topicOverride: topic, brokerOverride: broker);
    }

    public Task PublishManyAsync<T>(IEnumerable<T> messages, string topic, string broker)
    {
        return PublishCoreAsync(messages, topicOverride: topic, brokerOverride: broker);
    }

    private async Task PublishCoreAsync<T>(IEnumerable<T> messages, string? topicOverride, string? brokerOverride)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var resolvedRoute = _routeResolver.Resolve<T>();
        var topic = ChooseTopic(topicOverride, resolvedRoute.Topic);
        var broker = ChooseBroker(brokerOverride, resolvedRoute.Broker);

        var payloads = messages.Select(SerializeMessage).ToArray();
        if (payloads.Length == 0)
        {
            return;
        }

        var targets = ResolveTargets(broker);
        foreach (var target in targets)
        {
            var outbound = payloads
                .Select(payload => new OutboundMessage(
                    Body: payload,
                    Topic: topic,
                    Broker: target.Name,
                    Headers: EmptyHeaders,
                    MessageId: Guid.CreateVersion7(),
                    SentAtUtc: DateTimeOffset.UtcNow))
                .ToArray();

            if (outbound.Length == 1)
            {
                await target.SendAsync(outbound[0]).ConfigureAwait(false);
                continue;
            }

            await target.SendBatchAsync(outbound).ConfigureAwait(false);
        }
    }

    private static IEnumerable<T> Single<T>(T message)
    {
        return new[] { message };
    }

    private ReadOnlyMemory<byte> SerializeMessage<T>(T message)
    {
        if (message is null)
        {
            throw new ArgumentException("Message cannot be null.", nameof(message));
        }

        var json = JsonSerializer.Serialize(message, _serializerOptions);
        return Encoding.UTF8.GetBytes(json);
    }

    private IReadOnlyList<ITransport> ResolveTargets(string? broker)
    {
        if (broker is null)
        {
            return _transports;
        }

        if (!_transportsByName.TryGetValue(broker, out var target))
        {
            throw new ArgumentException($"Broker '{broker}' is not registered.", nameof(broker));
        }

        return new[] { target };
    }

    private static string ChooseTopic(string? explicitTopic, string defaultTopic)
    {
        if (explicitTopic is null)
        {
            return defaultTopic;
        }

        if (string.IsNullOrWhiteSpace(explicitTopic))
        {
            throw new ArgumentException("Topic cannot be empty or whitespace.", nameof(explicitTopic));
        }

        return explicitTopic;
    }

    private static string? ChooseBroker(string? explicitBroker, string? defaultBroker)
    {
        if (explicitBroker is null)
        {
            return defaultBroker;
        }

        if (string.IsNullOrWhiteSpace(explicitBroker))
        {
            throw new ArgumentException("Broker cannot be empty or whitespace.", nameof(explicitBroker));
        }

        return explicitBroker;
    }

    private static IReadOnlyDictionary<string, ITransport> BuildTransportLookup(IEnumerable<ITransport> transports)
    {
        var lookup = new Dictionary<string, ITransport>(StringComparer.OrdinalIgnoreCase);
        foreach (var transport in transports)
        {
            if (string.IsNullOrWhiteSpace(transport.Name))
            {
                throw new ArgumentException("Transport name cannot be empty or whitespace.", nameof(transports));
            }

            if (!lookup.TryAdd(transport.Name, transport))
            {
                throw new ArgumentException($"Duplicate transport name '{transport.Name}' detected.", nameof(transports));
            }
        }

        return lookup;
    }
}


