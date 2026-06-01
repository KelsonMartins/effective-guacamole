namespace Guacamole.QueueProcessor.Configuration;

/// <summary>
/// Configuration for a single queue runtime.
/// </summary>
public sealed class QueueRuntimeOptions
{
    /// <summary>
    /// Name of the Azure Storage Queue.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Minimum number of concurrent workers.
    /// Default: 1
    /// </summary>
    public int MinWorkers { get; set; } = 1;

    /// <summary>
    /// Maximum number of concurrent workers.
    /// Default: 10
    /// </summary>
    public int MaxWorkers { get; set; } = 10;

    /// <summary>
    /// Number of messages to fetch per batch.
    /// Default: 8 (Azure Queue max: 32)
    /// </summary>
    public int BatchSize { get; set; } = 8;

    /// <summary>
    /// Maximum capacity of the prefetch channel.
    /// Default: 1000
    /// </summary>
    public int ChannelCapacity { get; set; } = 1000;

    /// <summary>
    /// Visibility timeout in seconds for fetched messages.
    /// Default: 60
    /// </summary>
    public int VisibilityTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum number of dequeues before sending to dead-letter.
    /// Default: 5
    /// </summary>
    public int MaxDequeueCount { get; set; } = 5;

    /// <summary>
    /// Whether to enable adaptive scaling.
    /// Default: true
    /// </summary>
    public bool EnableAdaptiveScaling { get; set; } = true;

    /// <summary>
    /// Target lag threshold in seconds for scaling decisions.
    /// Default: 300 (5 minutes)
    /// </summary>
    public int TargetLagSeconds { get; set; } = 300;

    /// <summary>
    /// Target CPU utilization percentage (0-100).
    /// Default: 70
    /// </summary>
    public int TargetCpuPercent { get; set; } = 70;

    /// <summary>
    /// The .NET type name of the message (used for deserialization).
    /// Example: "MyApp.Messages.OrderPlaced, MyApp.Messages"
    /// </summary>
    public string? MessageType { get; set; }

    /// <summary>
    /// The .NET type name of the processor.
    /// Example: "MyApp.Processors.OrderProcessor, MyApp"
    /// </summary>
    public string? ProcessorType { get; set; }

    /// <summary>
    /// Name of the dead-letter queue (defaults to {Name}-poison).
    /// </summary>
    public string? DeadLetterQueueName { get; set; }

    /// <summary>
    /// Name of the retry queue (optional, used for durable retry).
    /// </summary>
    public string? RetryQueueName { get; set; }

    /// <summary>
    /// Delay in seconds before retrying failed messages.
    /// Only used if RetryQueueName is specified.
    /// </summary>
    public int RetryDelaySeconds { get; set; } = 60;

    /// <summary>
    /// Resilience and retry policy for this queue.
    /// Controls in-process retries and optional durable retry queue behaviour.
    /// </summary>
    public RetryOptions Retry { get; set; } = new();

    /// <summary>
    /// Maximum time to wait for a full batch before flushing a partial batch.
    /// Only relevant when using batch processors (for example, IQueueBatchProcessor&lt;TMessage&gt;).
    /// Default: 100ms
    /// </summary>
    public int BatchFlushTimeoutMs { get; set; } = 100;
}