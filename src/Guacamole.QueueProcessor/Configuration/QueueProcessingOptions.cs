namespace Guacamole.QueueProcessor.Configuration;

/// <summary>
/// Top-level configuration for the queue processing platform.
/// </summary>
public sealed class QueueProcessingOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "QueueProcessing";

    /// <summary>
    /// List of queue configurations.
    /// </summary>
    public List<QueueRuntimeOptions> Queues { get; set; } = [];

    /// <summary>
    /// Connection string for Azure Storage.
    /// </summary>
    public string? ConnectionString { get; set; }
}