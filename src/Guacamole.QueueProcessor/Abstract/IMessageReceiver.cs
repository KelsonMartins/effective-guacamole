using Guacamole.QueueProcessor.Models;

namespace Guacamole.QueueProcessor.Abstract
{

    /// <summary>
    /// Interface for receiving messages from a queue provider.
    /// </summary>
    public interface IMessageReceiver
    {
        Task<IReadOnlyList<MessageEnvelope>> ReceiveMessagesAsync(int maxMessages, CancellationToken cancellationToken);
        Task<int> GetApproximateMessageCountAsync(CancellationToken cancellationToken);
    }
}