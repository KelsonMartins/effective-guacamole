using Azure.Storage.Queues;
using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Models;

namespace Guacamole.QueueProcessor.Services.Azure;

/// <summary>
/// Azure Storage Queue implementation of visibility timeout updater.
/// </summary>
internal sealed class AzureVisibilityUpdater(QueueClient queueClient, int visibilityTimeoutSeconds) : IVisibilityUpdater
{
    private readonly QueueClient _queueClient = queueClient;
    private readonly TimeSpan _visibilityTimeout = TimeSpan.FromSeconds(visibilityTimeoutSeconds);

    public async Task UpdateVisibilityTimeoutAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        await _queueClient.UpdateMessageAsync(envelope.MessageId, envelope.PopReceipt, visibilityTimeout: _visibilityTimeout, cancellationToken: cancellationToken);
    }
}