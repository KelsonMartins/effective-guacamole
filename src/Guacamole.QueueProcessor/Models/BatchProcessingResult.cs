namespace Guacamole.QueueProcessor.Models;

/// <summary>
/// Per-message result within a batch operation.
/// </summary>
public sealed class BatchProcessingResult
{
    /// <summary>
    /// Per-message outcomes, keyed by <see cref="MessageEnvelope.MessageId"/>.
    /// Every message in the batch MUST have an entry.
    /// </summary>
    public required IReadOnlyDictionary<string, ProcessingResult> Results { get; init; }

    /// <summary>
    /// Creates a result where every message in <paramref name="messageIds"/> succeeded.
    /// </summary>
    public static BatchProcessingResult AllSucceeded(IEnumerable<string> messageIds)
        => new()
        {
            Results = messageIds.ToDictionary(id => id, _ => ProcessingResult.Successful())
        };

    /// <summary>
    /// Creates a result where every message in <paramref name="messageIds"/> failed.
    /// </summary>
    public static BatchProcessingResult AllFailed(IEnumerable<string> messageIds, string errorMessage, Exception? exception = null, bool shouldRetry = true)
        => new()
        {
            Results = messageIds.ToDictionary(
                id => id,
                _ => ProcessingResult.Failed(errorMessage, exception, shouldRetry))
        };
}
