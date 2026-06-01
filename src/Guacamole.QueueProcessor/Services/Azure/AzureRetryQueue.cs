using Azure.Storage.Queues;
using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Models;

namespace Guacamole.QueueProcessor.Services.Azure;

/// <summary>
/// Azure Storage Queue implementation of the durable retry queue.
/// Sends the original message body back to the queue with a visibility delay,
/// so the message will only become available after the configured delay elapses.
/// </summary>
internal sealed class AzureRetryQueue(QueueClient retryQueueClient) : IRetryQueue
{
    private readonly QueueClient _queueClient = retryQueueClient;

    public async Task ScheduleRetryAsync(MessageEnvelope envelope, TimeSpan delay, CancellationToken cancellationToken)
    {
        // Re-enqueue the original payload with a visibility delay
        var payload = envelope.Payload.ToArray();
        var binaryData = new BinaryData(payload);

        await _queueClient.SendMessageAsync(binaryData, visibilityTimeout: delay, cancellationToken: cancellationToken);
    }
}
