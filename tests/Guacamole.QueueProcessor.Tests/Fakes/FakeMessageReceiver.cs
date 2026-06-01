using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Models;

namespace Guacamole.QueueProcessor.Tests.Fakes;

/// <summary>
/// In-memory message receiver backed by a pre-loaded list of envelopes.
/// </summary>
public sealed class FakeMessageReceiver : IMessageReceiver
{
    private readonly Queue<MessageEnvelope> _messages;
    public int ApproximateCount { get; set; }
    public int ReceiveCallCount { get; private set; }

    public FakeMessageReceiver(IEnumerable<MessageEnvelope>? messages = null)
    {
        _messages = new Queue<MessageEnvelope>(messages ?? []);
        ApproximateCount = _messages.Count;
    }

    public Task<IReadOnlyList<MessageEnvelope>> ReceiveMessagesAsync(int maxMessages, CancellationToken cancellationToken)
    {
        ReceiveCallCount++;
        var result = new List<MessageEnvelope>();

        for (int i = 0; i < maxMessages && _messages.Count > 0; i++)
            result.Add(_messages.Dequeue());

        return Task.FromResult<IReadOnlyList<MessageEnvelope>>(result);
    }

    public Task<int> GetApproximateMessageCountAsync(CancellationToken cancellationToken)
        => Task.FromResult(ApproximateCount);

    public void Enqueue(MessageEnvelope envelope) => _messages.Enqueue(envelope);
}
