using Guacamole.QueueProcessor.Abstract;

namespace Guacamole.QueueProcessor;

/// <summary>
/// Provider-specific queue components created by <see cref="IQueueRuntimeFactory"/>.
/// Groups all dependencies needed by a queue runtime.
/// </summary>
public sealed class QueueComponents
{
    /// <summary>The message receiver (polls/subscribes to the queue).</summary>
    public required IMessageReceiver Receiver { get; init; }

    /// <summary>The message deleter (acknowledges/completes successfully processed messages).</summary>
    public required IMessageDeleter Deleter { get; init; }

    /// <summary>The poison message router (dead-letters unprocessable messages).</summary>
    public required IPoisonRouter PoisonRouter { get; init; }

    /// <summary>The visibility updater (extends in-flight lease during long processing).</summary>
    public required IVisibilityUpdater VisibilityUpdater { get; init; }

    /// <summary>
    /// Optional durable retry queue.
    /// When set, exhausted messages are requeued with a delay instead of dead-lettered.
    /// </summary>
    public IRetryQueue? RetryQueue { get; init; }
}
