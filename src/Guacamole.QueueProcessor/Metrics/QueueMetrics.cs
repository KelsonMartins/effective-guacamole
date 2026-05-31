using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Guacamole.QueueProcessor.Metrics;

/// <summary>
/// Metrics recorder for queue processing using System.Diagnostics.Metrics.
/// Follows OpenTelemetry semantic conventions.
/// </summary>
internal sealed class QueueMetrics
{
    private readonly Meter _meter;
    private readonly Counter<long> _messagesProcessed;
    private readonly Counter<long> _messagesFailed;
    private readonly Histogram<double> _processingDuration;
    private readonly ObservableGauge<int> _queueDepth;
    private readonly ObservableGauge<int> _activeWorkers;
    private readonly ObservableGauge<double> _queueLag;

    private int _currentQueueDepth;
    private int _currentActiveWorkers;
    private double _currentQueueLag;

    public QueueMetrics(string queueName)
    {
        _meter = new Meter("QueueProcessor", "1.0.0");

        var commonTags = new TagList
        {
            { "queue.name", queueName }
        };

        _messagesProcessed = _meter.CreateCounter<long>(
            "queue.messages.processed",
            unit: "{message}",
            description: "Number of messages successfully processed");

        _messagesFailed = _meter.CreateCounter<long>(
            "queue.messages.failed",
            unit: "{message}",
            description: "Number of messages that failed processing");

        _processingDuration = _meter.CreateHistogram<double>(
            "queue.processing.duration",
            unit: "ms",
            description: "Message processing duration in milliseconds");

        _queueDepth = _meter.CreateObservableGauge(
            "queue.depth",
            () => new Measurement<int>(_currentQueueDepth, commonTags),
            unit: "{message}",
            description: "Current number of messages in the queue");

        _activeWorkers = _meter.CreateObservableGauge(
            "queue.workers.active",
            () => new Measurement<int>(_currentActiveWorkers, commonTags),
            unit: "{worker}",
            description: "Current number of active workers");

        _queueLag = _meter.CreateObservableGauge(
            "queue.lag",
            () => new Measurement<double>(_currentQueueLag, commonTags),
            unit: "s",
            description: "Estimated queue lag in seconds");
    }

    public void RecordMessageProcessed()
    {
        _messagesProcessed.Add(1);
    }

    public void RecordMessageFailed()
    {
        _messagesFailed.Add(1);
    }

    public void RecordProcessingDuration(double durationMs)
    {
        _processingDuration.Record(durationMs);
    }

    public void UpdateQueueDepth(int depth)
    {
        _currentQueueDepth = depth;
    }

    public void UpdateActiveWorkers(int count)
    {
        _currentActiveWorkers = count;
    }

    public void UpdateQueueLag(double lagSeconds)
    {
        _currentQueueLag = lagSeconds;
    }
}