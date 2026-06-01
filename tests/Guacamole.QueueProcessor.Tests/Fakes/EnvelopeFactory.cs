using Guacamole.QueueProcessor.Models;
using System.Text;
using System.Text.Json;

namespace Guacamole.QueueProcessor.Tests.Fakes;

public static class EnvelopeFactory
{
    public static MessageEnvelope Create<T>(T payload, string? messageId = null, int dequeueCount = 1) where T : class
    {
        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);

        return new MessageEnvelope
        {
            MessageId = messageId ?? Guid.NewGuid().ToString(),
            PopReceipt = Guid.NewGuid().ToString(),
            DequeueCount = dequeueCount,
            Payload = bytes,
            InsertedOn = DateTimeOffset.UtcNow.AddMinutes(-1),
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(59)
        };
    }

    public static MessageEnvelope CreateWithRawPayload(string messageId, byte[] payload, int dequeueCount = 1)
        => new()
        {
            MessageId = messageId,
            PopReceipt = Guid.NewGuid().ToString(),
            DequeueCount = dequeueCount,
            Payload = payload
        };
}
