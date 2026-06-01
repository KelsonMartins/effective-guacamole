namespace Guacamole.QueueProcessor.Abstract;

/// <summary>
/// Factory for creating provider-specific queue components.
/// Each call may create fresh components (used when hot-reloading configuration).
/// </summary>
public interface IQueueRuntimeFactory
{
    /// <summary>
    /// Creates all provider-specific components needed to operate a single queue.
    /// </summary>
    /// <param name="queueName">The logical queue name as configured</param>
    QueueComponents CreateComponents(string queueName);
}