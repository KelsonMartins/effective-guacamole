namespace Guacamole.QueueProcessor.Abstract
{
    /// <summary>
    /// Factory for creating provider-specific queue components.
    /// </summary>
    public interface IQueueRuntimeFactory
    {
        (IMessageReceiver receiver, IMessageDeleter deleter, IPoisonRouter poisonRouter) CreateComponents(string queueName);
    }
}