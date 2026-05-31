using Azure.Storage.Queues;
using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Guacamole.QueueProcessor.Services.Azure;

/// <summary>
/// Azure Storage Queue implementation of poison message router.
/// Sends poison messages to a dead-letter queue.
/// </summary>
internal sealed class AzurePoisonRouter(QueueClient deadLetterQueueClient, ILogger<AzurePoisonRouter> logger) : IPoisonRouter
{
    private readonly QueueClient _deadLetterQueueClient = deadLetterQueueClient;
    private readonly ILogger<AzurePoisonRouter> _logger = logger;

    public async Task RouteToDeadLetterAsync(MessageEnvelope envelope, string reason, CancellationToken cancellationToken)
    {
        try
        {
            // Create a wrapper with metadata about why it was poisoned
            var poisonMessage = new
            {
                OriginalMessageId = envelope.MessageId,
                DequeueCount = envelope.DequeueCount,
                PoisonedAt = DateTimeOffset.UtcNow,
                Reason = reason,
                OriginalPayload = Convert.ToBase64String(envelope.Payload.ToArray())
            };

            var json = JsonSerializer.Serialize(poisonMessage);
            await _deadLetterQueueClient.SendMessageAsync(json, cancellationToken);

            _logger.LogWarning("Routed message {MessageId} to dead-letter queue. Reason: {Reason}", envelope.MessageId, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to route message {MessageId} to dead-letter queue", envelope.MessageId);
        }
    }
}