using System.Text;
using System.Text.Json;

namespace Bussy.Net;

/// <summary>
/// Default <see cref="IMessageSerializer"/> that serializes messages as UTF-8 JSON using
/// <see cref="System.Text.Json.JsonSerializer"/>.
/// </summary>
public sealed class JsonMessageSerializer : IMessageSerializer
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Initializes a new <see cref="JsonMessageSerializer"/> with the specified options.
    /// When <paramref name="options"/> is <see langword="null"/>, default <see cref="JsonSerializerOptions"/> are used.
    /// </summary>
    /// <param name="options">Optional JSON serializer options.</param>
    public JsonMessageSerializer(JsonSerializerOptions? options = null)
    {
        _options = options ?? new JsonSerializerOptions();
    }

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> Serialize<T>(T message)
    {
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, _options));
    }

    /// <inheritdoc/>
    public T? Deserialize<T>(ReadOnlyMemory<byte> data)
    {
        return JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(data.Span), _options);
    }
}
