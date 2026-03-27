using Bussy.Net.Test.TestMessageTypes;

namespace Bussy.Net.Test;

[TestFixture]
public sealed class MessageRouteResolverTests
{
    private readonly MessageRouteResolver _resolver = new();

    [Test]
    public void Resolve_TestMessage_UsesSimpleClassNameAndNullBroker()
    {
        var route = _resolver.Resolve<TestMessage>();

        Assert.Multiple(() =>
        {
            Assert.That(route.Topic, Is.EqualTo(nameof(TestMessage)));
            Assert.That(route.Broker, Is.Null);
        });
    }

    [Test]
    public void Resolve_TopicOnlyMessage_UsesConfiguredTopicAndNullBroker()
    {
        var route = _resolver.Resolve<TopicOnlyMessage>();

        Assert.Multiple(() =>
        {
            Assert.That(route.Topic, Is.EqualTo("topic-only"));
            Assert.That(route.Broker, Is.Null);
        });
    }

    [Test]
    public void Resolve_BrokerOnlyMessage_UsesClassNameAndConfiguredBroker()
    {
        var route = _resolver.Resolve<BrokerOnlyMessage>();

        Assert.Multiple(() =>
        {
            Assert.That(route.Topic, Is.EqualTo(nameof(BrokerOnlyMessage)));
            Assert.That(route.Broker, Is.EqualTo("sqs"));
        });
    }

    [Test]
    public void Resolve_TopicAndBrokerMessage_UsesConfiguredTopicAndBroker()
    {
        var route = _resolver.Resolve<TopicAndBrokerMessage>();

        Assert.Multiple(() =>
        {
            Assert.That(route.Topic, Is.EqualTo("orders-created"));
            Assert.That(route.Broker, Is.EqualTo("rabbitmq"));
        });
    }

    [Test]
    public void Resolve_InvalidTopicMessage_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => _resolver.Resolve<InvalidTopicMessage>());

        Assert.That(exception!.ParamName, Is.EqualTo("Topic"));
    }

    [Test]
    public void Resolve_InvalidBrokerMessage_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => _resolver.Resolve<InvalidBrokerMessage>());

        Assert.That(exception!.ParamName, Is.EqualTo("Broker"));
    }
}


