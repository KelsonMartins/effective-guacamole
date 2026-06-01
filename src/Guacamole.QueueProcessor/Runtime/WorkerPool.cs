using System.Threading.Channels;
using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Configuration;
using Guacamole.QueueProcessor.Models;
using Guacamole.QueueProcessor.Services.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;

namespace Guacamole.QueueProcessor.Runtime;

/// <summary>
/// Worker pool that consumes messages from a channel and executes processors.
/// Supports both single-message and batch processing modes, with an in-process
/// resilience pipeline (exponential back-off) and optional durable retry queue.
/// </summary>
internal sealed class WorkerPool(string queueName,
                                 Type messageType,
                                 Type processorType,
                                 bool isBatchProcessor,
                                 IServiceProvider serviceProvider,
                                 MessageDeserializer deserializer,
                                 IMessageDeleter messageDeleter,
                                 IPoisonRouter poisonRouter,
                                 IRetryQueue? retryQueue,
                                 ILogger<WorkerPool> logger,
                                 int maxDequeueCount,
                                 int batchSize,
                                 int batchFlushTimeoutMs,
                                 RetryOptions retryOptions)
{
    private readonly string _queueName = queueName;
    private readonly Type _messageType = messageType;
    private readonly Type _processorType = processorType;
    private readonly bool _isBatchProcessor = isBatchProcessor;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly MessageDeserializer _deserializer = deserializer;
    private readonly IMessageDeleter _messageDeleter = messageDeleter;
    private readonly IPoisonRouter _poisonRouter = poisonRouter;
    private readonly IRetryQueue? _retryQueue = retryQueue;
    private readonly ILogger<WorkerPool> _logger = logger;
    private readonly int _maxDequeueCount = maxDequeueCount;
    private readonly int _batchSize = batchSize;
    private readonly TimeSpan _batchFlushTimeout = TimeSpan.FromMilliseconds(batchFlushTimeoutMs);
    private readonly ResiliencePipeline _resiliencePipeline = BuildResiliencePipeline(retryOptions);
    private readonly RetryOptions _retryOptions = retryOptions;

    private int _activeWorkers;
    private int _targetWorkerCount;
    private readonly List<Task> _workerTasks = [];
    private readonly object _scaleLock = new();

    public int ActiveWorkerCount => _activeWorkers;

    /// <summary>Starts the worker pool with the specified number of workers.</summary>
    public void Start(ChannelReader<MessageEnvelope> channelReader, int initialWorkerCount, CancellationToken cancellationToken)
    {
        _targetWorkerCount = initialWorkerCount;

        for (int i = 0; i < initialWorkerCount; i++)
            StartWorker(channelReader, cancellationToken);
    }

    /// <summary>Adjusts the number of workers dynamically.</summary>
    public void ScaleWorkers(int targetCount)
    {
        lock (_scaleLock)
        {
            if (targetCount == _targetWorkerCount)
                return;

            _targetWorkerCount = targetCount;
            _logger.LogInformation("Scaling workers for queue {QueueName} to {TargetCount}", _queueName, targetCount);
        }
    }

    /// <summary>Waits for all workers to complete.</summary>
    public async Task WaitForCompletionAsync()
        => await Task.WhenAll(_workerTasks);

    // -------------------------------------------------------------------------
    // Worker lifecycle
    // -------------------------------------------------------------------------

    private void StartWorker(ChannelReader<MessageEnvelope> channelReader, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _activeWorkers);

        var workerTask = Task.Run(async () =>
        {
            try
            {
                if (_isBatchProcessor)
                    await WorkerBatchLoopAsync(channelReader, cancellationToken);
                else
                    await WorkerLoopAsync(channelReader, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _activeWorkers);
            }
        }, cancellationToken);

        lock (_scaleLock)
            _workerTasks.Add(workerTask);
    }

    // -------------------------------------------------------------------------
    // Single-message processing
    // -------------------------------------------------------------------------

    private async Task WorkerLoopAsync(ChannelReader<MessageEnvelope> channelReader, CancellationToken cancellationToken)
    {
        await foreach (var envelope in channelReader.ReadAllAsync(cancellationToken))
        {
            if (_activeWorkers > _targetWorkerCount)
            {
                _logger.LogDebug("Worker stopping due to scale-down for queue {QueueName}", _queueName);
                return;
            }

            await ProcessMessageAsync(envelope, cancellationToken);
        }
    }

    private async Task ProcessMessageAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        var processingStarted = DateTimeOffset.UtcNow;

        if (envelope.DequeueCount > _maxDequeueCount)
        {
            _logger.LogWarning("Message {MessageId} exceeded max dequeue count ({Max}), routing to dead-letter",
                envelope.MessageId, _maxDequeueCount);
            await _poisonRouter.RouteToDeadLetterAsync(envelope, "Max dequeue count exceeded", cancellationToken);
            await _messageDeleter.DeleteMessageAsync(envelope, cancellationToken);
            return;
        }

        object? message;
        try
        {
            message = _deserializer.Deserialize(envelope.Payload, _messageType);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or FormatException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Failed to deserialize message {MessageId} for queue {QueueName}", envelope.MessageId, _queueName);
            await _poisonRouter.RouteToDeadLetterAsync(envelope, "Deserialization failed", cancellationToken);
            await _messageDeleter.DeleteMessageAsync(envelope, cancellationToken);
            return;
        }

        if (message == null)
        {
            _logger.LogWarning("Failed to deserialize message {MessageId} for queue {QueueName}", envelope.MessageId, _queueName);
            await _poisonRouter.RouteToDeadLetterAsync(envelope, "Deserialization failed", cancellationToken);
            await _messageDeleter.DeleteMessageAsync(envelope, cancellationToken);
            return;
        }

        var context = BuildContext(envelope);
        ProcessingResult result;

        try
        {
            // Execute inside the resilience pipeline so transient exceptions are
            // automatically retried with exponential back-off.
            result = await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                using var scope = _serviceProvider.CreateScope();
                var processor = scope.ServiceProvider.GetService(_processorType)
                    ?? throw new InvalidOperationException($"Processor {_processorType.Name} not registered in DI");

                var processMethod = _processorType.GetMethod("ProcessAsync")
                    ?? throw new InvalidOperationException($"ProcessAsync not found on {_processorType.Name}");

                var resultTask = (Task<ProcessingResult>?)processMethod.Invoke(processor, [message, context, ct])
                    ?? throw new InvalidOperationException($"ProcessAsync returned null on {_processorType.Name}");

                return await resultTask;
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resilience pipeline exhausted for message {MessageId} on queue {QueueName}",
                envelope.MessageId, _queueName);
            result = ProcessingResult.Failed(ex.Message, ex, shouldRetry: true);
        }

        await HandleResultAsync(envelope, result, processingStarted, cancellationToken);
    }

    private async Task HandleResultAsync(MessageEnvelope envelope, ProcessingResult result, DateTimeOffset processingStarted, CancellationToken cancellationToken)
    {
        if (result.Success)
        {
            await _messageDeleter.DeleteMessageAsync(envelope, cancellationToken);
            var duration = DateTimeOffset.UtcNow - processingStarted;
            _logger.LogInformation("Processed message {MessageId} from {QueueName} in {DurationMs}ms",
                envelope.MessageId, _queueName, (int)duration.TotalMilliseconds);
            return;
        }

        _logger.LogWarning("Failed to process message {MessageId} from {QueueName}: {Error}",
            envelope.MessageId, _queueName, result.ErrorMessage);

        if (!result.ShouldRetry)
        {
            await _poisonRouter.RouteToDeadLetterAsync(envelope, result.ErrorMessage ?? "Processing failed", cancellationToken);
            await _messageDeleter.DeleteMessageAsync(envelope, cancellationToken);
            return;
        }

        // Durable retry: requeue with a visibility delay instead of dead-lettering
        if (_retryOptions.EnableDurableRetry && _retryQueue is not null)
        {
            await _retryQueue.ScheduleRetryAsync(envelope, _retryOptions.DurableRetryDelay, cancellationToken);
            await _messageDeleter.DeleteMessageAsync(envelope, cancellationToken);
            _logger.LogInformation("Message {MessageId} scheduled for durable retry in {Delay}",
                envelope.MessageId, _retryOptions.DurableRetryDelay);
            return;
        }

        // Let the queue's visibility timeout expire so Azure re-delivers automatically
        _logger.LogDebug("Message {MessageId} will be retried via visibility timeout", envelope.MessageId);
    }

    // -------------------------------------------------------------------------
    // Batch processing
    // -------------------------------------------------------------------------

    private async Task WorkerBatchLoopAsync(ChannelReader<MessageEnvelope> channelReader, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_activeWorkers > _targetWorkerCount)
            {
                _logger.LogDebug("Batch worker stopping due to scale-down for queue {QueueName}", _queueName);
                return;
            }

            var batch = await CollectBatchAsync(channelReader, _batchSize, _batchFlushTimeout, cancellationToken);
            if (batch.Count > 0)
                await ProcessBatchAsync(batch, cancellationToken);
        }
    }

    private static async Task<List<MessageEnvelope>> CollectBatchAsync(ChannelReader<MessageEnvelope> reader, int maxSize, TimeSpan flushTimeout, CancellationToken cancellationToken)
    {
        var batch = new List<MessageEnvelope>(maxSize);

        // Block until at least one message arrives or cancellation
        try
        {
            if (!await reader.WaitToReadAsync(cancellationToken))
                return batch;
        }
        catch (OperationCanceledException)
        {
            return batch;
        }

        // Drain up to maxSize with a timeout for accumulation
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(flushTimeout);

        try
        {
            while (batch.Count < maxSize && reader.TryRead(out var envelope))
                batch.Add(envelope);

            while (batch.Count < maxSize)
            {
                if (!await reader.WaitToReadAsync(cts.Token))
                    break;

                while (batch.Count < maxSize && reader.TryRead(out var envelope))
                    batch.Add(envelope);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Flush timeout elapsed - process partial batch
        }

        return batch;
    }

    private async Task ProcessBatchAsync(List<MessageEnvelope> envelopes, CancellationToken cancellationToken)
    {
        // Filter out poison messages
        var valid = new List<MessageEnvelope>(envelopes.Count);
        foreach (var envelope in envelopes)
        {
            if (envelope.DequeueCount > _maxDequeueCount)
            {
                await _poisonRouter.RouteToDeadLetterAsync(envelope, "Max dequeue count exceeded", cancellationToken);
                await _messageDeleter.DeleteMessageAsync(envelope, cancellationToken);
            }
            else
            {
                valid.Add(envelope);
            }
        }

        if (valid.Count == 0)
            return;

        // Deserialize and build BatchItem<TMessage> list via reflection
        var batchItemType = typeof(BatchItem<>).MakeGenericType(_messageType);
        var listType = typeof(List<>).MakeGenericType(batchItemType);
        var typedList = Activator.CreateInstance(listType)!;
        var addMethod = listType.GetMethod("Add")!;
        var messageProperty = batchItemType.GetProperty("Message")!;
        var contextProperty = batchItemType.GetProperty("Context")!;
        var envelopeProperty = batchItemType.GetProperty("Envelope")!;

        var processableEnvelopes = new List<MessageEnvelope>(valid.Count);

        foreach (var envelope in valid)
        {
            object? message;
            try
            {
                message = _deserializer.Deserialize(envelope.Payload, _messageType);
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or FormatException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Failed to deserialize message {MessageId} for queue {QueueName}", envelope.MessageId, _queueName);
                await _poisonRouter.RouteToDeadLetterAsync(envelope, "Deserialization failed", cancellationToken);
                await _messageDeleter.DeleteMessageAsync(envelope, cancellationToken);
                continue;
            }

            if (message == null)
            {
                await _poisonRouter.RouteToDeadLetterAsync(envelope, "Deserialization failed", cancellationToken);
                await _messageDeleter.DeleteMessageAsync(envelope, cancellationToken);
                continue;
            }

            var item = Activator.CreateInstance(batchItemType)!;
            messageProperty.SetValue(item, message);
            contextProperty.SetValue(item, BuildContext(envelope));
            envelopeProperty.SetValue(item, envelope);
            addMethod.Invoke(typedList, [item]);
            processableEnvelopes.Add(envelope);
        }

        if (processableEnvelopes.Count == 0)
            return;

        BatchProcessingResult batchResult;

        try
        {
            batchResult = await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                using var scope = _serviceProvider.CreateScope();
                var processor = scope.ServiceProvider.GetService(_processorType)
                    ?? throw new InvalidOperationException($"Batch processor {_processorType.Name} not registered in DI");

                var processMethod = _processorType.GetMethod("ProcessBatchAsync")
                    ?? throw new InvalidOperationException($"ProcessBatchAsync not found on {_processorType.Name}");

                var resultTask = (Task<BatchProcessingResult>?)processMethod.Invoke(processor, [typedList, ct])
                    ?? throw new InvalidOperationException($"ProcessBatchAsync returned null on {_processorType.Name}");

                return await resultTask;
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resilience pipeline exhausted for batch of {Count} messages on queue {QueueName}",
                processableEnvelopes.Count, _queueName);
            batchResult = BatchProcessingResult.AllFailed(
                processableEnvelopes.Select(e => e.MessageId), ex.Message, ex, shouldRetry: true);
        }

        foreach (var envelope in processableEnvelopes)
        {
            if (!batchResult.Results.TryGetValue(envelope.MessageId, out var result))
            {
                _logger.LogWarning("Batch processor did not return result for message {MessageId}", envelope.MessageId);
                continue;
            }

            await HandleResultAsync(envelope, result, DateTimeOffset.UtcNow, cancellationToken);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private ProcessingContext BuildContext(MessageEnvelope envelope) => new()
    {
        MessageId = envelope.MessageId,
        DequeueCount = envelope.DequeueCount,
        QueueName = _queueName,
        InsertedOn = envelope.InsertedOn,
        ExpiresOn = envelope.ExpiresOn,
        PopReceipt = envelope.PopReceipt
    };

    private static ResiliencePipeline BuildResiliencePipeline(RetryOptions options)
    {
        if (options.MaxAttempts <= 0)
            return ResiliencePipeline.Empty;

        return new ResiliencePipelineBuilder()
            .AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = options.MaxAttempts,
                Delay = options.InitialDelay,
                MaxDelay = options.MaxDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = options.UseJitter,
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => ex is not OperationCanceledException)
            })
            .Build();
    }
}
