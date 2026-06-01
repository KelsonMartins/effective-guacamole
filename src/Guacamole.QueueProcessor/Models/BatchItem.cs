namespace Guacamole.QueueProcessor.Models;

/// <summary>
/// A single item in a batch, pairing the deserialized message with its processing context.
/// </summary>
/// <typeparam name="TMessage">The message type</typeparam>
public sealed class BatchItem<TMessage> where TMessage : class
{
    /// <summary>The deserialized message payload.</summary>
    public required TMessage Message { get; init; }

    /// <summary>Processing metadata for this message.</summary>
    public required ProcessingContext Context { get; init; }

    /// <summary>The raw envelope (available for advanced scenarios).</summary>
    public required MessageEnvelope Envelope { get; init; }
}
