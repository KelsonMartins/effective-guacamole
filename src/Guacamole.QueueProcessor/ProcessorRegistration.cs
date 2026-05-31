namespace Guacamole.QueueProcessor
{
    /// <summary>
    /// Registration details for a processor.
    /// </summary>
    public sealed class ProcessorRegistration
    {
        public required string QueueName { get; init; }
        public required Type MessageType { get; init; }
        public required Type ProcessorType { get; init; }
    }
}