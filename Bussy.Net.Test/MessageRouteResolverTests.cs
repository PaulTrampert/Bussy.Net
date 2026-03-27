using System;

namespace Bussy.Net.Test;

[TestFixture]
public sealed class MessageRouteResolverTests
{
    private readonly MessageRouteResolver _resolver = new();

    [Test]
    public void Resolve_NoAttribute_UsesSimpleClassNameAndNullBroker()
    {
        var route = _resolver.Resolve<NoAttributeMessage>();

        Assert.Multiple(() =>
        {
            Assert.That(route.Topic, Is.EqualTo(nameof(NoAttributeMessage)));
            Assert.That(route.Broker, Is.Null);
        });
    }

    [Test]
    public void Resolve_AttributeWithTopicOnly_UsesAttributeTopicAndNullBroker()
    {
        var route = _resolver.Resolve<TopicOnlyMessage>();

        Assert.Multiple(() =>
        {
            Assert.That(route.Topic, Is.EqualTo("topic-only"));
            Assert.That(route.Broker, Is.Null);
        });
    }

    [Test]
    public void Resolve_AttributeWithNullTopicAndBroker_UsesClassNameAndAttributeBroker()
    {
        var route = _resolver.Resolve<BrokerOnlyMessage>();

        Assert.Multiple(() =>
        {
            Assert.That(route.Topic, Is.EqualTo(nameof(BrokerOnlyMessage)));
            Assert.That(route.Broker, Is.EqualTo("kafka"));
        });
    }

    [Test]
    public void Resolve_AttributeWithTopicAndBroker_UsesBothAttributeValues()
    {
        var route = _resolver.Resolve<TopicAndBrokerMessage>();

        Assert.Multiple(() =>
        {
            Assert.That(route.Topic, Is.EqualTo("orders-created"));
            Assert.That(route.Broker, Is.EqualTo("rabbitmq"));
        });
    }

    [Test]
    public void Resolve_InvalidTopic_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => _resolver.Resolve<InvalidTopicMessage>());

        Assert.That(exception!.ParamName, Is.EqualTo("Topic"));
    }

    [Test]
    public void Resolve_InvalidBroker_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => _resolver.Resolve<InvalidBrokerMessage>());

        Assert.That(exception!.ParamName, Is.EqualTo("Broker"));
    }

    private sealed class NoAttributeMessage;

    [MessageRoute("topic-only")]
    private sealed class TopicOnlyMessage;

    [MessageRoute(Broker = "kafka")]
    private sealed class BrokerOnlyMessage;

    [MessageRoute("orders-created", "rabbitmq")]
    private sealed class TopicAndBrokerMessage;

    [MessageRoute(" ")]
    private sealed class InvalidTopicMessage;

    [MessageRoute(Broker = "   ")]
    private sealed class InvalidBrokerMessage;
}


