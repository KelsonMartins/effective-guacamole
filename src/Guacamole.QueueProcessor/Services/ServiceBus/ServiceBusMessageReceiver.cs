using Azure.Messaging.ServiceBus;
using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Models;

namespace Guacamole.QueueProcessor.Services.ServiceBus;

/// <summary>
/// Holds the Service Bus receiver alongside the received message.
/// Stored in <see cref="MessageEnvelope.OriginalMessage"/> so the
/// deleter and poison router can call Complete/DeadLetter without
/// holding a separate reference.
/// </summary>
internal sealed class ServiceBusMessageContext
{
    public required ServiceBusReceivedMessage Message { get; init; }
    public required ServiceBusReceiver Receiver { get; init; }
}

/// <summary>
/// Azure Service Bus implementation of message receiver.
/// </summary>
internal sealed class ServiceBusMessageReceiver(ServiceBusReceiver receiver) : IMessageReceiver
{
    private readonly ServiceBusReceiver _receiver = receiver;

    public async Task<IReadOnlyList<MessageEnvelope>> ReceiveMessagesAsync(int maxMessages, CancellationToken cancellationToken)
    {
        var messages = await _receiver.ReceiveMessagesAsync(maxMessages, cancellationToken: cancellationToken);

        var envelopes = new List<MessageEnvelope>(messages.Count);
        foreach (var message in messages)
        {
            envelopes.Add(new MessageEnvelope
            {
                MessageId = message.MessageId,
                PopReceipt = message.LockToken,
                DequeueCount = (int)message.DeliveryCount,
                Payload = message.Body.ToMemory(),
                InsertedOn = message.EnqueuedTime,
                ExpiresOn = message.ExpiresAt,
                OriginalMessage = new ServiceBusMessageContext { Message = message, Receiver = _receiver }
            });
        }

        return envelopes;
    }

    public async Task<int> GetApproximateMessageCountAsync(CancellationToken cancellationToken)
    {
        // Service Bus doesn't expose a direct count via ServiceBusReceiver;
        // return -1 to signal "unknown" to the auto-scaler.
        await Task.CompletedTask;
        return -1;
    }
}
