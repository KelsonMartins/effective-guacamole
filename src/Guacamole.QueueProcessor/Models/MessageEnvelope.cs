namespace Guacamole.QueueProcessor.Models;

/// <summary>
/// Lightweight envelope wrapping a queue message with metadata.
/// Minimizes allocations by using pooled byte arrays for payload.
/// </summary>
public sealed class MessageEnvelope
{
    /// <summary>
    /// Unique message identifier.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// Pop receipt for message deletion and visibility updates.
    /// </summary>
    public required string PopReceipt { get; init; }

    /// <summary>
    /// Number of times this message has been dequeued.
    /// </summary>
    public required int DequeueCount { get; init; }

    /// <summary>
    /// Binary message payload (may be pooled).
    /// </summary>
    public required ReadOnlyMemory<byte> Payload { get; init; }

    /// <summary>
    /// When the message was inserted.
    /// </summary>
    public DateTimeOffset? InsertedOn { get; init; }

    /// <summary>
    /// When the message expires.
    /// </summary>
    public DateTimeOffset? ExpiresOn { get; init; }

    /// <summary>
    /// Original queue message reference (provider-specific).
    /// </summary>
    public object? OriginalMessage { get; init; }
}