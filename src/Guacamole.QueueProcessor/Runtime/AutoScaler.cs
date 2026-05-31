using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Configuration;
using Microsoft.Extensions.Logging;

namespace Guacamole.QueueProcessor.Runtime;

/// <summary>
/// Automatically scales worker count based on queue lag and CPU utilization.
/// </summary>
internal sealed class AutoScaler(string queueName, IMessageReceiver messageReceiver, WorkerPool workerPool, ILogger<AutoScaler> logger, QueueRuntimeOptions options, int initialWorkerCount)
{
    private readonly string _queueName = queueName;
    private readonly IMessageReceiver _messageReceiver = messageReceiver;
    private readonly WorkerPool _workerPool = workerPool;
    private readonly ILogger<AutoScaler> _logger = logger;
    private readonly QueueRuntimeOptions _options = options;

    private int _currentWorkerCount = initialWorkerCount;
    private DateTime _lastScaleTime = DateTime.UtcNow;
    private readonly TimeSpan _scaleInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Runs the auto-scaling loop.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!_options.EnableAdaptiveScaling)
        {
            _logger.LogInformation("Adaptive scaling disabled for queue {QueueName}", _queueName);
            return;
        }

        _logger.LogInformation("Starting auto-scaler for queue {QueueName}", _queueName);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_scaleInterval, cancellationToken);

                // Prevent scaling too frequently
                if (DateTime.UtcNow - _lastScaleTime < _scaleInterval)
                    continue;

                await EvaluateScalingAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in auto-scaler for queue {QueueName}", _queueName);
        }

        _logger.LogInformation("Auto-scaler stopped for queue {QueueName}", _queueName);
    }

    private async Task EvaluateScalingAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Get current queue depth
            var queueDepth = await _messageReceiver.GetApproximateMessageCountAsync(cancellationToken);

            // Simple scaling logic based on queue depth
            var desiredWorkerCount = CalculateDesiredWorkerCount(queueDepth);

            if (desiredWorkerCount != _currentWorkerCount)
            {
                _logger.LogInformation("Scaling queue {QueueName} from {CurrentWorkers} to {DesiredWorkers} workers (queue depth: {QueueDepth})", _queueName, _currentWorkerCount, desiredWorkerCount, queueDepth);

                _workerPool.ScaleWorkers(desiredWorkerCount);
                _currentWorkerCount = desiredWorkerCount;
                _lastScaleTime = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating scaling for queue {QueueName}", _queueName);
        }
    }

    private int CalculateDesiredWorkerCount(int queueDepth)
    {
        // Scale based on queue depth
        // This is a simple algorithm - can be enhanced with CPU metrics, processing rate, etc.

        if (queueDepth == 0)
            return _options.MinWorkers;

        // Scale up if there's significant backlog
        // Rule: 1 worker per 100 messages, but respect min/max bounds
        var desiredWorkers = Math.Max(_options.MinWorkers, queueDepth / 100);
        desiredWorkers = Math.Min(desiredWorkers, _options.MaxWorkers);

        // Gradual scaling - don't change by more than 50% at once
        var maxChange = Math.Max(1, _currentWorkerCount / 2);

        if (desiredWorkers > _currentWorkerCount)
        {
            return Math.Min(desiredWorkers, _currentWorkerCount + maxChange);
        }
        else if (desiredWorkers < _currentWorkerCount)
        {
            return Math.Max(desiredWorkers, _currentWorkerCount - maxChange);
        }

        return _currentWorkerCount;
    }
}