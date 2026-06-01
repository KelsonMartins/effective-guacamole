using Guacamole.QueueProcessor.Models;

namespace Guacamole.QueueProcessor.Abstract;

/// <summary>
/// Defines a batch message processor for a specific message type.
/// Implement when your business logic can process multiple messages more
/// efficiently together (e.g. bulk database inserts, batched API calls).
/// </summary>
/// <typeparam name="TMessage">The strongly-typed message to process</typeparam>
public interface IQueueBatchProcessor<TMessage> where TMessage : class
{
    /// <summary>
    /// Process a batch of messages from the queue.
    /// </summary>
    /// <param name="batch">The batch of deserialized messages with their contexts</param>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown</param>
    /// <returns>Per-message processing results keyed by MessageId</returns>
    Task<BatchProcessingResult> ProcessBatchAsync(IReadOnlyList<BatchItem<TMessage>> batch, CancellationToken cancellationToken);
}
