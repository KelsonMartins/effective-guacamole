namespace Guacamole.QueueProcessor;

/// <summary>
/// Provides context and metadata about the current message being processed.
/// </summary>
public sealed class ProcessingContext
{
    /// <summary>
    /// Unique identifier for the message.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// Number of times this message has been dequeued.
    /// </summary>
    public required int DequeueCount { get; init; }

    /// <summary>
    /// The name of the queue this message came from.
    /// </summary>
    public required string QueueName { get; init; }

    /// <summary>
    /// When the message was first inserted into the queue.
    /// </summary>
    public DateTimeOffset? InsertedOn { get; init; }

    /// <summary>
    /// When the message will become visible again if not deleted.
    /// </summary>
    public DateTimeOffset? ExpiresOn { get; init; }

    /// <summary>
    /// Receipt handle required for message deletion and visibility updates.
    /// For internal framework use - processors should not use this directly.
    /// </summary>
    public required string PopReceipt { get; init; }
}