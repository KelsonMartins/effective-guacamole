using Guacamole.QueueProcessor.Models;

namespace Guacamole.QueueProcessor.Abstract
{
    /// <summary>
    /// Interface for updating message visibility timeout.
    /// </summary>
    public interface IVisibilityUpdater
    {
        Task UpdateVisibilityTimeoutAsync(MessageEnvelope envelope, CancellationToken cancellationToken);
    }
}