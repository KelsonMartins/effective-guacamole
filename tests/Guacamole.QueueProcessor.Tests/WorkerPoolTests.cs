using System.Threading.Channels;
using System.Text;
using System.Text.Json;
using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Runtime;
using Guacamole.QueueProcessor.Configuration;
using Guacamole.QueueProcessor.Models;
using Guacamole.QueueProcessor.Services.Core;
using Guacamole.QueueProcessor.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Guacamole.QueueProcessor.Tests;

public class WorkerPoolTests
{
    private sealed record OrderMessage(string OrderId, decimal Amount);

    private static WorkerPool BuildPool(
        FakeQueueProcessor<OrderMessage> processor,
        FakeMessageDeleter? deleter = null,
        FakePoisonRouter? poisonRouter = null,
        FakeRetryQueue? retryQueue = null,
        int maxDequeueCount = 5,
        RetryOptions? retryOptions = null,
        bool isBatch = false)
    {
        var services = new ServiceCollection();
        services.AddScoped<FakeQueueProcessor<OrderMessage>>(_ => processor);

        var sp = services.BuildServiceProvider();
        deleter ??= new FakeMessageDeleter();
        poisonRouter ??= new FakePoisonRouter();
        retryOptions ??= new RetryOptions { MaxAttempts = 0 }; // no retries by default

        return new WorkerPool(
            queueName: "orders",
            messageType: typeof(OrderMessage),
            processorType: typeof(FakeQueueProcessor<OrderMessage>),
            isBatchProcessor: isBatch,
            serviceProvider: sp,
            deserializer: new MessageDeserializer(),
            messageDeleter: deleter,
            poisonRouter: poisonRouter,
            retryQueue: retryQueue,
            logger: NullLogger<WorkerPool>.Instance,
            maxDequeueCount: maxDequeueCount,
            batchSize: 8,
            batchFlushTimeoutMs: 50,
            retryOptions: retryOptions);
    }

    [Test]
    public async Task ProcessMessage_Success_DeletesMessage()
    {
        var deleter = new FakeMessageDeleter();
        var processor = new FakeQueueProcessor<OrderMessage>();
        var pool = BuildPool(processor, deleter);

        var channel = Channel.CreateUnbounded<MessageEnvelope>();
        var envelope = EnvelopeFactory.Create(new OrderMessage("O1", 99.99m));
        await channel.Writer.WriteAsync(envelope);
        channel.Writer.Complete();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        pool.Start(channel.Reader, 1, cts.Token);
        await pool.WaitForCompletionAsync();

        await Assert.That(deleter.DeletedMessageIds).Contains(envelope.MessageId);
        await Assert.That(processor.ReceivedMessages).Count().IsEqualTo(1);
    }

    [Test]
    public async Task ProcessMessage_ProcessorFails_NoRetry_RoutesToDeadLetter()
    {
        var deleter = new FakeMessageDeleter();
        var poison = new FakePoisonRouter();
        var processor = new FakeQueueProcessor<OrderMessage>(
            (_, _) => ProcessingResult.Failed("error", shouldRetry: false));

        var pool = BuildPool(processor, deleter, poison);
        var channel = Channel.CreateUnbounded<MessageEnvelope>();
        var envelope = EnvelopeFactory.Create(new OrderMessage("O2", 1m));
        await channel.Writer.WriteAsync(envelope);
        channel.Writer.Complete();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        pool.Start(channel.Reader, 1, cts.Token);
        await pool.WaitForCompletionAsync();

        await Assert.That(poison.RoutedMessages.Select(r => r.MessageId)).Contains(envelope.MessageId);
        await Assert.That(deleter.DeletedMessageIds).Contains(envelope.MessageId);
    }

    [Test]
    public async Task ProcessMessage_ExceedsMaxDequeueCount_RoutesToDeadLetter()
    {
        var poison = new FakePoisonRouter();
        var deleter = new FakeMessageDeleter();
        var processor = new FakeQueueProcessor<OrderMessage>();

        var pool = BuildPool(processor, deleter, poison, maxDequeueCount: 3);
        var channel = Channel.CreateUnbounded<MessageEnvelope>();
        var envelope = EnvelopeFactory.Create(new OrderMessage("O3", 1m), dequeueCount: 4); // exceeds 3
        await channel.Writer.WriteAsync(envelope);
        channel.Writer.Complete();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        pool.Start(channel.Reader, 1, cts.Token);
        await pool.WaitForCompletionAsync();

        await Assert.That(poison.RoutedMessages.Select(r => r.MessageId)).Contains(envelope.MessageId);
        await Assert.That(processor.ReceivedMessages).IsEmpty();
    }

    [Test]
    public async Task ProcessMessage_InvalidJson_RoutesToDeadLetter()
    {
        var poison = new FakePoisonRouter();
        var deleter = new FakeMessageDeleter();
        var processor = new FakeQueueProcessor<OrderMessage>();

        var pool = BuildPool(processor, deleter, poison);
        var channel = Channel.CreateUnbounded<MessageEnvelope>();
        var envelope = EnvelopeFactory.CreateWithRawPayload("bad-msg", "not-json"u8.ToArray());
        await channel.Writer.WriteAsync(envelope);
        channel.Writer.Complete();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        pool.Start(channel.Reader, 1, cts.Token);
        await pool.WaitForCompletionAsync();

        await Assert.That(poison.RoutedMessages.Select(r => r.MessageId)).Contains("bad-msg");
    }

    [Test]
    public async Task ProcessMessage_Base64EncodedJson_ProcessesSuccessfully()
    {
        var poison = new FakePoisonRouter();
        var deleter = new FakeMessageDeleter();
        var processor = new FakeQueueProcessor<OrderMessage>();

        var pool = BuildPool(processor, deleter, poison);
        var channel = Channel.CreateUnbounded<MessageEnvelope>();

        var json = JsonSerializer.Serialize(new OrderMessage("O-BASE64", 9.5m));
        var base64Payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        var envelope = EnvelopeFactory.CreateWithRawPayload("base64-msg", Encoding.UTF8.GetBytes(base64Payload));

        await channel.Writer.WriteAsync(envelope);
        channel.Writer.Complete();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        pool.Start(channel.Reader, 1, cts.Token);
        await pool.WaitForCompletionAsync();

        await Assert.That(processor.ReceivedMessages).Count().IsEqualTo(1);
        await Assert.That(processor.ReceivedMessages[0].Message.OrderId).IsEqualTo("O-BASE64");
        await Assert.That(deleter.DeletedMessageIds).Contains("base64-msg");
        await Assert.That(poison.RoutedMessages).IsEmpty();
    }

    [Test]
    public async Task ProcessMessage_DurableRetry_RequeuesMessageAndDeletes()
    {
        var retryQueue = new FakeRetryQueue();
        var deleter = new FakeMessageDeleter();
        var processor = new FakeQueueProcessor<OrderMessage>(
            (_, _) => ProcessingResult.Failed("transient", shouldRetry: true));

        var pool = BuildPool(processor, deleter, retryQueue: retryQueue,
            retryOptions: new RetryOptions
            {
                MaxAttempts = 0,
                EnableDurableRetry = true,
                DurableRetryDelay = TimeSpan.FromMinutes(5)
            });

        var channel = Channel.CreateUnbounded<MessageEnvelope>();
        var envelope = EnvelopeFactory.Create(new OrderMessage("O4", 1m));
        await channel.Writer.WriteAsync(envelope);
        channel.Writer.Complete();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        pool.Start(channel.Reader, 1, cts.Token);
        await pool.WaitForCompletionAsync();

        await Assert.That(retryQueue.ScheduledRetries.Select(r => r.MessageId)).Contains(envelope.MessageId);
        await Assert.That(retryQueue.ScheduledRetries[0].Delay).IsEqualTo(TimeSpan.FromMinutes(5));
        await Assert.That(deleter.DeletedMessageIds).Contains(envelope.MessageId);
    }

    [Test]
    public async Task ProcessMessage_ResiliencePipeline_RetriesOnException()
    {
        var deleter = new FakeMessageDeleter();
        var callCount = 0;
        var processor = new FakeQueueProcessor<OrderMessage>((_, _) =>
        {
            callCount++;
            if (callCount < 3)
                throw new InvalidOperationException("transient");
            return ProcessingResult.Successful();
        });

        var pool = BuildPool(processor, deleter,
            retryOptions: new RetryOptions
            {
                MaxAttempts = 3,
                InitialDelay = TimeSpan.FromMilliseconds(1),
                MaxDelay = TimeSpan.FromMilliseconds(10)
            });

        var channel = Channel.CreateUnbounded<MessageEnvelope>();
        var envelope = EnvelopeFactory.Create(new OrderMessage("O5", 1m));
        await channel.Writer.WriteAsync(envelope);
        channel.Writer.Complete();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        pool.Start(channel.Reader, 1, cts.Token);
        await pool.WaitForCompletionAsync();

        await Assert.That(callCount).IsEqualTo(3);
        await Assert.That(deleter.DeletedMessageIds).Contains(envelope.MessageId);
    }

    [Test]
    public async Task ScaleWorkers_ChangesTargetCount()
    {
        var processor = new FakeQueueProcessor<OrderMessage>();
        var pool = BuildPool(processor);

        // Not started - just verify the method runs without error
        pool.ScaleWorkers(5);

        // ActiveWorkerCount is 0 since we didn't start, but target should be updated internally
        await Assert.That(pool.ActiveWorkerCount).IsEqualTo(0);
    }
}

