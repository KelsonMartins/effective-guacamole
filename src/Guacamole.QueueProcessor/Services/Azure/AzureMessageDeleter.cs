using Azure.Storage.Queues;
using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Models;

namespace Guacamole.QueueProcessor.Services.Azure;

/// <summary>
/// Azure Storage Queue implementation of message deleter.
/// </summary>
internal sealed class AzureMessageDeleter(QueueClient queueClient) : IMessageDeleter
{
    private readonly QueueClient _queueClient = queueClient;

    public async Task DeleteMessageAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        await _queueClient.DeleteMessageAsync(envelope.MessageId, envelope.PopReceipt, cancellationToken);
    }
}