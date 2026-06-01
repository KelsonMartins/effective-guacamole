using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Models;
using RabbitMQ.Client;

namespace Guacamole.QueueProcessor.Services.RabbitMq;

/// <summary>
/// Holds the RabbitMQ channel and delivery tag alongside a received message.
/// Stored in <see cref="MessageEnvelope.OriginalMessage"/> so the
/// deleter and poison router can ack/nack without a separate reference.
/// </summary>
internal sealed class RabbitMqMessageContext
{
    public required IChannel Channel { get; init; }
    public required ulong DeliveryTag { get; init; }
}

/// <summary>
/// RabbitMQ implementation of message receiver.
/// </summary>
internal sealed class RabbitMqMessageReceiver(IChannel channel, string queueName) : IMessageReceiver
{
    private readonly IChannel _channel = channel;
    private readonly string _queueName = queueName;

    public async Task<IReadOnlyList<MessageEnvelope>> ReceiveMessagesAsync(int maxMessages, CancellationToken cancellationToken)
    {
        var envelopes = new List<MessageEnvelope>(maxMessages);

        for (int i = 0; i < maxMessages; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _channel.BasicGetAsync(_queueName, autoAck: false, cancellationToken);
            if (result is null)
                break;

            envelopes.Add(new MessageEnvelope
            {
                MessageId = result.BasicProperties.MessageId ?? result.DeliveryTag.ToString(),
                PopReceipt = result.DeliveryTag.ToString(),
                DequeueCount = (int)(result.BasicProperties.Headers?.TryGetValue("x-delivery-count", out var count) == true
                    ? (long)(count ?? 0L) : 0L),
                Payload = result.Body.ToArray(),
                InsertedOn = null,
                ExpiresOn = null,
                OriginalMessage = new RabbitMqMessageContext
                {
                    Channel = _channel,
                    DeliveryTag = result.DeliveryTag
                }
            });
        }

        return envelopes;
    }

    public async Task<int> GetApproximateMessageCountAsync(CancellationToken cancellationToken)
    {
        var queueInfo = await _channel.QueueDeclarePassiveAsync(_queueName, cancellationToken);
        return (int)queueInfo.MessageCount;
    }
}
