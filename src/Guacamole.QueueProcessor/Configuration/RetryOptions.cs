namespace Guacamole.QueueProcessor.Configuration;

/// <summary>
/// Resilience and retry configuration for a single queue runtime.
/// Controls both in-process retries (via a resilience pipeline) and
/// durable retries (via a dedicated retry queue with visibility delay).
/// </summary>
public sealed class RetryOptions
{
    /// <summary>
    /// Maximum number of in-process retry attempts before escalating.
    /// Default: 3
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Initial delay between the first and second attempt.
    /// Subsequent delays grow exponentially up to <see cref="MaxDelay"/>.
    /// Default: 200ms
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Maximum per-attempt delay regardless of the exponential growth.
    /// Default: 30s
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Adds random jitter to delay calculations to avoid thundering-herd issues.
    /// Default: true
    /// </summary>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    /// When true, messages that exhaust in-process retries are placed onto the
    /// durable retry queue with a <see cref="DurableRetryDelay"/> delay rather
    /// than being dead-lettered immediately.
    /// Default: false
    /// </summary>
    public bool EnableDurableRetry { get; set; } = false;

    /// <summary>
    /// Visibility delay applied when a message is placed on the durable retry queue.
    /// Default: 1 minute
    /// </summary>
    public TimeSpan DurableRetryDelay { get; set; } = TimeSpan.FromMinutes(1);
}
