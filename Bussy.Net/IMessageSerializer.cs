namespace Bussy.Net;

/// <summary>
/// Abstracts message serialization and deserialization, allowing custom formats to be plugged in.
/// Implement this interface and register it in the DI container to replace the default JSON serializer.
/// </summary>
public interface IMessageSerializer
{
    /// <summary>
    /// Serializes <paramref name="message"/> to a byte representation.
    /// </summary>
    /// <typeparam name="T">The message type.</typeparam>
    /// <param name="message">The message to serialize.</param>
    /// <returns>The serialized bytes.</returns>
    ReadOnlyMemory<byte> Serialize<T>(T message);

    /// <summary>
    /// Deserializes a message from its byte representation.
    /// </summary>
    /// <typeparam name="T">The expected message type.</typeparam>
    /// <param name="data">The raw bytes to deserialize.</param>
    /// <returns>The deserialized message, or <see langword="null"/> if deserialization yields null.</returns>
    T? Deserialize<T>(ReadOnlyMemory<byte> data);
}
