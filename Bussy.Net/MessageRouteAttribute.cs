using System;

namespace Bussy.Net;

/// <summary>
/// Declares default routing metadata for a message type.
/// </summary>
/// <remarks>
/// Any explicit topic or broker supplied at publish time should take precedence over this attribute.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class MessageRouteAttribute : Attribute
{
    private string? _topic;
    private string? _broker;

    /// <summary>
    /// Initializes a new message attribute.
    /// </summary>
    public MessageRouteAttribute()
    {
    }

    /// <summary>
    /// Initializes a new message attribute with a topic override.
    /// </summary>
    /// <param name="topic">Default topic for this message type.</param>
    public MessageRouteAttribute(string topic)
    {
        Topic = topic;
    }

    /// <summary>
    /// Initializes a new message attribute with topic and broker overrides.
    /// </summary>
    /// <param name="topic">Default topic for this message type.</param>
    /// <param name="broker">Default broker for this message type.</param>
    public MessageRouteAttribute(string topic, string broker)
    {
        Topic = topic;
        Broker = broker;
    }

    /// <summary>
    /// Gets or sets the default topic for this message type.
    /// </summary>
    public string? Topic
    {
        get => _topic;
        set => _topic = ValidateOrNull(value, nameof(Topic));
    }

    /// <summary>
    /// Gets or sets the default broker for this message type.
    /// </summary>
    public string? Broker
    {
        get => _broker;
        set => _broker = ValidateOrNull(value, nameof(Broker));
    }

    private static string? ValidateOrNull(string? value, string parameterName)
    {
        if (value is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty or whitespace.", parameterName);
        }

        return value;
    }
}

