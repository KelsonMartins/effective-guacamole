using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Models;
using Microsoft.Extensions.Logging;

namespace Guacamole.QueueProcessor.Services.ServiceBus;

/// <summary>
/// Azure Service Bus implementation of poison message router.
/// Dead-letters the message with a reason string.
/// </summary>
internal sealed class ServiceBusPoisonRouter(ILogger<ServiceBusPoisonRouter> logger) : IPoisonRouter
{
    private readonly ILogger<ServiceBusPoisonRouter> _logger = logger;

    public async Task RouteToDeadLetterAsync(MessageEnvelope envelope, string reason, CancellationToken cancellationToken)
    {
        var context = GetContext(envelope);

        try
        {
            await context.Receiver.DeadLetterMessageAsync(
                context.Message,
                deadLetterReason: reason,
                cancellationToken: cancellationToken);

            _logger.LogWarning("Dead-lettered message {MessageId}. Reason: {Reason}", envelope.MessageId, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dead-letter message {MessageId}", envelope.MessageId);
        }
    }

    private static ServiceBusMessageContext GetContext(MessageEnvelope envelope)
        => envelope.OriginalMessage as ServiceBusMessageContext
           ?? throw new InvalidOperationException("MessageEnvelope.OriginalMessage is not a ServiceBusMessageContext");
}
