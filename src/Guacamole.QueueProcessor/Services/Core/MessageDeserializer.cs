using System.Text.Json;

namespace Guacamole.QueueProcessor.Services.Core;

/// <summary>
/// Message deserializer using System.Text.Json with minimal allocations.
/// </summary>
internal sealed class MessageDeserializer
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Deserialize message payload to strongly-typed message.
    /// </summary>
    public static T? Deserialize<T>(ReadOnlyMemory<byte> payload) where T : class
    {
        if (payload.IsEmpty)
            return null;

        return JsonSerializer.Deserialize<T>(payload.Span, DefaultOptions);
    }

    /// <summary>
    /// Deserialize message payload to a specific type.
    /// </summary>
    public object? Deserialize(ReadOnlyMemory<byte> payload, Type messageType)
    {
        if (payload.IsEmpty)
            return null;

        return JsonSerializer.Deserialize(payload.Span, messageType, DefaultOptions);
    }
}