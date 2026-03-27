using System;
using System.Reflection;

namespace Bussy.Net;

internal readonly record struct MessageRoute(string Topic, string? Broker);

internal sealed class MessageRouteResolver
{
    public MessageRoute Resolve<T>()
    {
        return Resolve(typeof(T));
    }

    public MessageRoute Resolve(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);

        var topic = messageType.Name;
        string? broker = null;

        MessageRouteAttribute? attribute;
        try
        {
            attribute = messageType.GetCustomAttribute<MessageRouteAttribute>(inherit: false);
        }
        catch (CustomAttributeFormatException ex) when (ex.InnerException is TargetInvocationException { InnerException: ArgumentException argumentException })
        {
            throw argumentException;
        }

        if (attribute is not null)
        {
            topic = attribute.Topic ?? topic;
            broker = attribute.Broker;
        }

        return new MessageRoute(topic, broker);
    }
}


