namespace Guacamole.QueueProcessor;

/// <summary>
/// Registration details for a processor (single-message or batch).
/// </summary>
public sealed class ProcessorRegistration
{
    public required string QueueName { get; init; }
    public required Type MessageType { get; init; }
    public required Type ProcessorType { get; init; }

    /// <summary>
    /// When true, <see cref="ProcessorType"/> implements
    /// <c>IQueueBatchProcessor&lt;TMessage&gt;</c> rather than
    /// <c>IQueueProcessor&lt;TMessage&gt;</c>.
    /// </summary>
    public bool IsBatchProcessor { get; init; }
}
