namespace Guacamole.QueueProcessor.Configuration;

/// <summary>
/// Top-level configuration for the queue processing platform.
/// Supports hot reload: changes take effect on the next runtime restart cycle.
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
    /// Connection string for Azure Service Bus (used by the Service Bus provider).
    /// </summary>
    public string? ServiceBusConnectionString { get; set; }

    /// <summary>
    /// RabbitMQ connection URI (used by the RabbitMQ provider).
    /// Example: amqp://user:pass@host:5672/vhost
    /// </summary>
    public string? RabbitMqUri { get; set; }
}