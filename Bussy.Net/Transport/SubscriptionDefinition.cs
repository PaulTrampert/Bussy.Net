namespace Bussy.Net.Transport;

/// <summary>
/// Describes what a transport adapter should subscribe to.
/// </summary>
/// <param name="Name">A logical subscription name used for diagnostics.</param>
/// <param name="Topic">Topic, queue, or route to consume from.</param>
/// <param name="Broker">Logical broker identifier.</param>
public sealed record SubscriptionDefinition(
    string Name,
    string Topic,
    string Broker
);

