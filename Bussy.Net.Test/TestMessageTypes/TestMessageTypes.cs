namespace Bussy.Net.Test.TestMessageTypes;

public sealed record TestMessage(string Name, int Count);

[MessageRoute("topic-only")]
public sealed record TopicOnlyMessage;

[MessageRoute(Broker = "sqs")]
public sealed record BrokerOnlyMessage;

[MessageRoute("orders-created", "rabbitmq")]
public sealed record TopicAndBrokerMessage;

[MessageRoute(" ")]
public sealed record InvalidTopicMessage;

[MessageRoute(Broker = "   ")]
public sealed record InvalidBrokerMessage;

