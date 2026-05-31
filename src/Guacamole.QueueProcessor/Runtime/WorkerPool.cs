using System.Threading.Channels;
using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Models;
using Guacamole.QueueProcessor.Services.Core;
using Microsoft.Extensions.Logging;

namespace Guacamole.QueueProcessor.Runtime;

/// <summary>
/// Worker pool that consumes messages from a channel and executes processors.
/// </summary>
internal sealed class WorkerPool(string queueName, Type messageType, Type processorType, IServiceProvider serviceProvider, MessageDeserializer deserializer, IMessageDeleter messageDeleter, IPoisonRouter poisonRouter, ILogger<WorkerPool> logger, int maxDequeueCount)
{
    private readonly string _queueName = queueName;
    private readonly Type _messageType = messageType;
    private readonly Type _processorType = processorType;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly MessageDeserializer _deserializer = deserializer;
    private readonly IMessageDeleter _messageDeleter = messageDeleter;
    private readonly IPoisonRouter _poisonRouter = poisonRouter;
    private readonly ILogger<WorkerPool> _logger = logger;
    private readonly int _maxDequeueCount = maxDequeueCount;

    private int _activeWorkers;
    private int _targetWorkerCount;
    private readonly List<Task> _workerTasks = [];
    private readonly object _scaleLock = new();

    public int ActiveWorkerCount => _activeWorkers;

    /// <summary>
    /// Starts the worker pool with the specified number of workers.
    /// </summary>
    public void Start(ChannelReader<MessageEnvelope> channelReader, int initialWorkerCount, CancellationToken cancellationToken)
    {
        _targetWorkerCount = initialWorkerCount;

        for (int i = 0; i < initialWorkerCount; i++)
            StartWorker(channelReader, cancellationToken);
    }

    /// <summary>
    /// Adjusts the number of workers dynamically.
    /// </summary>
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

    /// <summary>
    /// Waits for all workers to complete.
    /// </summary>
    public async Task WaitForCompletionAsync()
    {
        await Task.WhenAll(_workerTasks);
    }

    private void StartWorker(ChannelReader<MessageEnvelope> channelReader, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _activeWorkers);

        var workerTask = Task.Run(async () =>
        {
            try
            {
                await WorkerLoopAsync(channelReader, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _activeWorkers);
            }
        }, cancellationToken);

        lock (_scaleLock)
        {
            _workerTasks.Add(workerTask);
        }
    }

    private async Task WorkerLoopAsync(ChannelReader<MessageEnvelope> channelReader, CancellationToken cancellationToken)
    {
        await foreach (var envelope in channelReader.ReadAllAsync(cancellationToken))
        {
            // Check if we should stop this worker (scale down)
            if (_activeWorkers > _targetWorkerCount)
            {
                _logger.LogDebug("Worker stopping due to scale down for queue {QueueName}", _queueName);
                return;
            }

            await ProcessMessageAsync(envelope, cancellationToken);
        }
    }

    private async Task ProcessMessageAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        var processingStarted = DateTimeOffset.UtcNow;

        try
        {
            // Check for poison message
            if (envelope.DequeueCount > _maxDequeueCount)
            {
                await _poisonRouter.RouteToDeadLetterAsync(envelope, "Max dequeue count exceeded", cancellationToken);
                await _messageDeleter.DeleteMessageAsync(envelope, cancellationToken);
                return;
            }

            // Deserialize message
            var message = _deserializer.Deserialize(envelope.Payload, _messageType);
            if (message == null)
            {
                _logger.LogWarning("Failed to deserialize message {MessageId} for queue {QueueName}", envelope.MessageId, _queueName);
                await _poisonRouter.RouteToDeadLetterAsync(envelope, "Deserialization failed", cancellationToken);
                await _messageDeleter.DeleteMessageAsync(envelope, cancellationToken);
                return;
            }

            // Build processing context
            var context = new ProcessingContext
            {
                MessageId = envelope.MessageId,
                DequeueCount = envelope.DequeueCount,
                QueueName = _queueName,
                InsertedOn = envelope.InsertedOn,
                ExpiresOn = envelope.ExpiresOn,
                PopReceipt = envelope.PopReceipt
            };

            // Get processor from DI
            var processor = _serviceProvider.GetService(_processorType);
            if (processor == null)
            {
                _logger.LogError("Processor {ProcessorType} not found in DI for queue {QueueName}", _processorType.Name, _queueName);
                return;
            }

            // Invoke processor
            var processMethod = _processorType.GetMethod("ProcessAsync");
            if (processMethod == null)
            {
                _logger.LogError("ProcessAsync method not found on processor {ProcessorType}", _processorType.Name);
                return;
            }

            var resultTask = (Task<ProcessingResult>?)processMethod.Invoke(processor, [message, context, cancellationToken]);

            if (resultTask == null)
            {
                _logger.LogError("ProcessAsync returned null for processor {ProcessorType}", _processorType.Name);
                return;
            }

            var result = await resultTask;

            // Handle result
            if (result.Success)
            {
                await _messageDeleter.DeleteMessageAsync(envelope, cancellationToken);

                var duration = DateTimeOffset.UtcNow - processingStarted;
                _logger.LogInformation("Successfully processed message {MessageId} from queue {QueueName} in {Duration}ms", envelope.MessageId, _queueName, duration.TotalMilliseconds);
            }
            else
            {
                _logger.LogWarning("Failed to process message {MessageId} from queue {QueueName}: {Error}", envelope.MessageId, _queueName, result.ErrorMessage);

                if (!result.ShouldRetry)
                {
                    await _poisonRouter.RouteToDeadLetterAsync(envelope, result.ErrorMessage ?? "Processing failed", cancellationToken);
                    await _messageDeleter.DeleteMessageAsync(envelope, cancellationToken);
                }
                // If ShouldRetry is true, message will become visible again automatically
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing message {MessageId} from queue {QueueName}", envelope.MessageId, _queueName);
        }
    }
}
