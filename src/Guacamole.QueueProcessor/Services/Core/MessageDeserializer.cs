using System.Text;
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

        try
        {
            return JsonSerializer.Deserialize<T>(payload.Span, DefaultOptions);
        }
        catch (JsonException)
        {
            // Some queue producers store JSON as Base64 text (for example Azure queue interop).
            // Fallback to Base64 decode and deserialize the decoded JSON payload.
            if (TryDecodeBase64(payload.Span, out var decoded))
                return JsonSerializer.Deserialize<T>(decoded, DefaultOptions);

            throw;
        }
    }

    /// <summary>
    /// Deserialize message payload to a specific type.
    /// </summary>
    public object? Deserialize(ReadOnlyMemory<byte> payload, Type messageType)
    {
        if (payload.IsEmpty)
            return null;

        try
        {
            return JsonSerializer.Deserialize(payload.Span, messageType, DefaultOptions);
        }
        catch (JsonException)
        {
            // Some queue producers store JSON as Base64 text (for example Azure queue interop).
            // Fallback to Base64 decode and deserialize the decoded JSON payload.
            if (TryDecodeBase64(payload.Span, out var decoded))
                return JsonSerializer.Deserialize(decoded, messageType, DefaultOptions);

            throw;
        }
    }

    private static bool TryDecodeBase64(ReadOnlySpan<byte> payload, out byte[] decoded)
    {
        decoded = [];

        if (payload.IsEmpty || payload.Length % 4 != 0)
            return false;

        foreach (var b in payload)
        {
            if (IsBase64Byte(b) || char.IsWhiteSpace((char)b))
                continue;

            return false;
        }

        try
        {
            var text = Encoding.UTF8.GetString(payload);
            decoded = Convert.FromBase64String(text);
            return decoded.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsBase64Byte(byte b)
        => (b >= (byte)'A' && b <= (byte)'Z')
           || (b >= (byte)'a' && b <= (byte)'z')
           || (b >= (byte)'0' && b <= (byte)'9')
           || b == (byte)'+'
           || b == (byte)'/'
           || b == (byte)'=';
}