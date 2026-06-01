using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Models;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Guacamole.QueueProcessor.Services.RabbitMq;

/// <summary>
/// RabbitMQ implementation of poison message router.
/// Nacks the message without requeue, which routes it to the dead-letter exchange
/// if the queue has one configured, or discards it otherwise.
/// </summary>
internal sealed class RabbitMqPoisonRouter(ILogger<RabbitMqPoisonRouter> logger) : IPoisonRouter
{
    private readonly ILogger<RabbitMqPoisonRouter> _logger = logger;

    public async Task RouteToDeadLetterAsync(MessageEnvelope envelope, string reason, CancellationToken cancellationToken)
    {
        var context = GetContext(envelope);

        try
        {
            // nack with requeue:false — routes to DLX if configured
            await context.Channel.BasicNackAsync(context.DeliveryTag, multiple: false, requeue: false, cancellationToken);
            _logger.LogWarning("Nacked message {MessageId} (DLX). Reason: {Reason}", envelope.MessageId, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to nack message {MessageId}", envelope.MessageId);
        }
    }

    private static RabbitMqMessageContext GetContext(MessageEnvelope envelope)
        => envelope.OriginalMessage as RabbitMqMessageContext
           ?? throw new InvalidOperationException("MessageEnvelope.OriginalMessage is not a RabbitMqMessageContext");
}
