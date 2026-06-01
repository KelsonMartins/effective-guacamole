namespace Guacamole.QueueProcessor.Services.Core;

/// <summary>
/// Registry for mapping queue names to message processors.
/// Built at startup for O(1) lookup during processing.
/// </summary>
public sealed class ProcessorRegistry
{
    private readonly Dictionary<string, ProcessorRegistration> _registrations = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a processor for a queue.
    /// </summary>
    public void Register(string queueName, Type messageType, Type processorType, bool isBatchProcessor = false)
    {
        if (_registrations.ContainsKey(queueName))
            throw new InvalidOperationException($"Queue '{queueName}' already has a registered processor.");

        _registrations[queueName] = new ProcessorRegistration
        {
            QueueName = queueName,
            MessageType = messageType,
            ProcessorType = processorType,
            IsBatchProcessor = isBatchProcessor
        };
    }

    /// <summary>
    /// Gets processor registration for a queue.
    /// </summary>
    public ProcessorRegistration? GetRegistration(string queueName)
    {
        _registrations.TryGetValue(queueName, out var registration);
        return registration;
    }

    /// <summary>
    /// Checks if a queue has a registered processor.
    /// </summary>
    public bool HasRegistration(string queueName)
        => _registrations.ContainsKey(queueName);

    /// <summary>
    /// Returns all registered queue names.
    /// </summary>
    public IReadOnlyCollection<string> GetQueueNames()
        => _registrations.Keys.ToList().AsReadOnly();
}
