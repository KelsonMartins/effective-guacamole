using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Models;

namespace Guacamole.QueueProcessor.Services.ServiceBus;

/// <summary>
/// Azure Service Bus implementation of visibility updater.
/// Renews the message lock to keep it invisible to other consumers during long processing.
/// </summary>
internal sealed class ServiceBusVisibilityUpdater : IVisibilityUpdater
{
    public async Task UpdateVisibilityTimeoutAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        var context = GetContext(envelope);
        await context.Receiver.RenewMessageLockAsync(context.Message, cancellationToken);
    }

    private static ServiceBusMessageContext GetContext(MessageEnvelope envelope)
        => envelope.OriginalMessage as ServiceBusMessageContext
           ?? throw new InvalidOperationException("MessageEnvelope.OriginalMessage is not a ServiceBusMessageContext");
}
