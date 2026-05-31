using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Models;

namespace Guacamole.QueueProcessor.Services.Azure;

/// <summary>
/// Azure Storage Queue implementation of message receiver.
/// </summary>
internal sealed class AzureMessageReceiver(QueueClient queueClient, int visibilityTimeoutSeconds) : IMessageReceiver
{
    private readonly QueueClient _queueClient = queueClient;
    private readonly TimeSpan _visibilityTimeout = TimeSpan.FromSeconds(visibilityTimeoutSeconds);

    public async Task<IReadOnlyList<MessageEnvelope>> ReceiveMessagesAsync(int maxMessages, CancellationToken cancellationToken)
    {
        var response = await _queueClient.ReceiveMessagesAsync(maxMessages: maxMessages, visibilityTimeout: _visibilityTimeout, cancellationToken: cancellationToken);

        var envelopes = new List<MessageEnvelope>();

        if (response.Value != null)
            foreach (var message in response.Value)
                envelopes.Add(ToEnvelope(message));

        return envelopes;
    }

    public async Task<int> GetApproximateMessageCountAsync(CancellationToken cancellationToken)
    {
        var properties = await _queueClient.GetPropertiesAsync(cancellationToken);
        return (int)properties.Value.ApproximateMessagesCount;
    }

    private static MessageEnvelope ToEnvelope(QueueMessage message)
    {
        // Convert message text to bytes
        var payloadBytes = message.Body.ToMemory();

        return new MessageEnvelope
        {
            MessageId = message.MessageId,
            PopReceipt = message.PopReceipt,
            DequeueCount = (int)message.DequeueCount,
            Payload = payloadBytes,
            InsertedOn = message.InsertedOn,
            ExpiresOn = message.ExpiresOn,
            OriginalMessage = message
        };
    }
}