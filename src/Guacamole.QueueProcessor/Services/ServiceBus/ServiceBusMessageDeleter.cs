using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Models;

namespace Guacamole.QueueProcessor.Services.ServiceBus;

/// <summary>
/// Azure Service Bus implementation of message deleter (complete).
/// </summary>
internal sealed class ServiceBusMessageDeleter : IMessageDeleter
{
    public async Task DeleteMessageAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        var context = GetContext(envelope);
        await context.Receiver.CompleteMessageAsync(context.Message, cancellationToken);
    }

    private static ServiceBusMessageContext GetContext(MessageEnvelope envelope)
        => envelope.OriginalMessage as ServiceBusMessageContext
           ?? throw new InvalidOperationException("MessageEnvelope.OriginalMessage is not a ServiceBusMessageContext");
}
