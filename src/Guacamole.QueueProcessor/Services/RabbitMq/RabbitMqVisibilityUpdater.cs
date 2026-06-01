using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Models;

namespace Guacamole.QueueProcessor.Services.RabbitMq;

/// <summary>
/// RabbitMQ implementation of visibility updater.
/// RabbitMQ does not support visibility timeout extensions natively,
/// so this is a no-op. Configure a sufficiently long consumer timeout
/// in the broker instead.
/// </summary>
internal sealed class RabbitMqVisibilityUpdater : IVisibilityUpdater
{
    public Task UpdateVisibilityTimeoutAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
