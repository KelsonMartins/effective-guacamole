using System.Threading.Channels;
using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Runtime;
using Guacamole.QueueProcessor.Configuration;
using Guacamole.QueueProcessor.Models;
using Guacamole.QueueProcessor.Services.Core;
using Guacamole.QueueProcessor.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Guacamole.QueueProcessor.Tests;

public class BatchProcessingTests
{
    private sealed record InvoiceMessage(string InvoiceId, decimal Total);

    private static WorkerPool BuildBatchPool(
        FakeQueueBatchProcessor<InvoiceMessage> processor,
        FakeMessageDeleter? deleter = null,
        int batchSize = 5,
        int batchFlushTimeoutMs = 50)
    {
        var services = new ServiceCollection();
        services.AddScoped<FakeQueueBatchProcessor<InvoiceMessage>>(_ => processor);

        var sp = services.BuildServiceProvider();
        deleter ??= new FakeMessageDeleter();

        return new WorkerPool(
            queueName: "invoices",
            messageType: typeof(InvoiceMessage),
            processorType: typeof(FakeQueueBatchProcessor<InvoiceMessage>),
            isBatchProcessor: true,
            serviceProvider: sp,
            deserializer: new MessageDeserializer(),
            messageDeleter: deleter,
            poisonRouter: new FakePoisonRouter(),
            retryQueue: null,
            logger: NullLogger<WorkerPool>.Instance,
            maxDequeueCount: 5,
            batchSize: batchSize,
            batchFlushTimeoutMs: batchFlushTimeoutMs,
            retryOptions: new RetryOptions { MaxAttempts = 0 });
    }

    [Test]
    public async Task BatchProcessor_DeliversAllItemsInOneBatch()
    {
        var processor = new FakeQueueBatchProcessor<InvoiceMessage>();
        var deleter = new FakeMessageDeleter();
        var pool = BuildBatchPool(processor, deleter, batchSize: 3, batchFlushTimeoutMs: 50);

        var channel = Channel.CreateUnbounded<MessageEnvelope>();
        var envelopes = new[]
        {
            EnvelopeFactory.Create(new InvoiceMessage("I1", 10m)),
            EnvelopeFactory.Create(new InvoiceMessage("I2", 20m)),
            EnvelopeFactory.Create(new InvoiceMessage("I3", 30m)),
        };

        foreach (var e in envelopes)
            await channel.Writer.WriteAsync(e);
        channel.Writer.Complete();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        pool.Start(channel.Reader, 1, cts.Token);
        await pool.WaitForCompletionAsync();

        await Assert.That(processor.ReceivedBatches).Count().IsEqualTo(1);
        await Assert.That(processor.ReceivedBatches[0]).Count().IsEqualTo(3);
    }

    [Test]
    public async Task BatchProcessor_DeletesAllProcessedMessages()
    {
        var processor = new FakeQueueBatchProcessor<InvoiceMessage>();
        var deleter = new FakeMessageDeleter();
        var pool = BuildBatchPool(processor, deleter, batchSize: 3, batchFlushTimeoutMs: 50);

        var channel = Channel.CreateUnbounded<MessageEnvelope>();
        var envelopes = Enumerable.Range(1, 3)
            .Select(i => EnvelopeFactory.Create(new InvoiceMessage($"I{i}", i * 10m)))
            .ToList();

        foreach (var e in envelopes)
            await channel.Writer.WriteAsync(e);
        channel.Writer.Complete();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        pool.Start(channel.Reader, 1, cts.Token);
        await pool.WaitForCompletionAsync();

        foreach (var env in envelopes)
            await Assert.That(deleter.DeletedMessageIds).Contains(env.MessageId);
    }

    [Test]
    public async Task BatchProcessor_FlushTimeout_ProcessesPartialBatch()
    {
        // We write 2 messages but batch size is 10 — flush timeout should trigger
        var processor = new FakeQueueBatchProcessor<InvoiceMessage>();
        var pool = BuildBatchPool(processor, batchSize: 10, batchFlushTimeoutMs: 100);

        var channel = Channel.CreateUnbounded<MessageEnvelope>();
        await channel.Writer.WriteAsync(EnvelopeFactory.Create(new InvoiceMessage("I1", 10m)));
        await channel.Writer.WriteAsync(EnvelopeFactory.Create(new InvoiceMessage("I2", 20m)));
        channel.Writer.Complete();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        pool.Start(channel.Reader, 1, cts.Token);
        await pool.WaitForCompletionAsync();

        await Assert.That(processor.ReceivedBatches).Count().IsGreaterThan(0);
        var totalMessages = processor.ReceivedBatches.Sum(b => b.Count);
        await Assert.That(totalMessages).IsEqualTo(2);
    }
}

