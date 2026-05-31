namespace Guacamole.QueueProcessor.Abstract;

/// <summary>
/// Defines a message processor for a specific message type.
/// Application teams implement this interface to provide business logic.
/// </summary>
/// <typeparam name="TMessage">The strongly-typed message to process</typeparam>
public interface IQueueProcessor<TMessage> where TMessage : class
{
    /// <summary>
    /// Process a single message from the queue.
    /// </summary>
    /// <param name="message">The deserialized message payload</param>
    /// <param name="context">Processing context containing metadata</param>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown</param>
    /// <returns>A result indicating success or failure</returns>
    Task<ProcessingResult> ProcessAsync(TMessage message, ProcessingContext context, CancellationToken cancellationToken);
}