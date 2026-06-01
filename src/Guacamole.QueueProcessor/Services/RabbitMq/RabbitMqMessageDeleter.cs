using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Models;

namespace Guacamole.QueueProcessor.Services.RabbitMq;

/// <summary>
/// RabbitMQ implementation of message deleter (basic.ack).
/// </summary>
internal sealed class RabbitMqMessageDeleter : IMessageDeleter
{
    public async Task DeleteMessageAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        var context = GetContext(envelope);
        await context.Channel.BasicAckAsync(context.DeliveryTag, multiple: false, cancellationToken);
    }

    private static RabbitMqMessageContext GetContext(MessageEnvelope envelope)
        => envelope.OriginalMessage as RabbitMqMessageContext
           ?? throw new InvalidOperationException("MessageEnvelope.OriginalMessage is not a RabbitMqMessageContext");
}
