using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Models;

namespace Guacamole.QueueProcessor.Tests.Fakes;

public sealed class FakeQueueProcessor<TMessage> : IQueueProcessor<TMessage> where TMessage : class
{
    private readonly Func<TMessage, ProcessingContext, ProcessingResult>? _handler;
    public List<(TMessage Message, ProcessingContext Context)> ReceivedMessages { get; } = [];

    public FakeQueueProcessor(Func<TMessage, ProcessingContext, ProcessingResult>? handler = null)
        => _handler = handler;

    public Task<ProcessingResult> ProcessAsync(TMessage message, ProcessingContext context, CancellationToken cancellationToken)
    {
        ReceivedMessages.Add((message, context));
        var result = _handler?.Invoke(message, context) ?? ProcessingResult.Successful();
        return Task.FromResult(result);
    }
}

public sealed class FakeQueueBatchProcessor<TMessage> : IQueueBatchProcessor<TMessage> where TMessage : class
{
    private readonly Func<IReadOnlyList<BatchItem<TMessage>>, BatchProcessingResult>? _handler;
    public List<IReadOnlyList<BatchItem<TMessage>>> ReceivedBatches { get; } = [];

    public FakeQueueBatchProcessor(Func<IReadOnlyList<BatchItem<TMessage>>, BatchProcessingResult>? handler = null)
        => _handler = handler;

    public Task<BatchProcessingResult> ProcessBatchAsync(
        IReadOnlyList<BatchItem<TMessage>> batch,
        CancellationToken cancellationToken)
    {
        ReceivedBatches.Add(batch);
        var result = _handler?.Invoke(batch)
            ?? BatchProcessingResult.AllSucceeded(batch.Select(b => b.Context.MessageId));
        return Task.FromResult(result);
    }
}
