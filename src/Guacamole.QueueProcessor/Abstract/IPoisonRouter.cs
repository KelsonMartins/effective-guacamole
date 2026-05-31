using Guacamole.QueueProcessor.Models;

namespace Guacamole.QueueProcessor.Abstract
{
    /// <summary>
    /// Interface for routing poison messages to dead-letter queue.
    /// </summary>
    public interface IPoisonRouter
    {
        Task RouteToDeadLetterAsync(MessageEnvelope envelope, string reason, CancellationToken cancellationToken);
    }
}