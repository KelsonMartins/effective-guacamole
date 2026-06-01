using Guacamole.QueueProcessor.Models;

namespace Guacamole.QueueProcessor.Abstract
{
    /// <summary>
    /// Interface for deleting messages from the queue.
    /// </summary>
    public interface IMessageDeleter
    {
        Task DeleteMessageAsync(MessageEnvelope envelope, CancellationToken cancellationToken);
    }
}