using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Configuration;
using Guacamole.QueueProcessor.Models;
using Guacamole.QueueProcessor.Services.Core;

namespace Guacamole.QueueProcessor.Runtime;

/// <summary>
/// Runtime orchestrator for a single queue.
/// Manages receiver, workers, channel, scaler, and visibility renewal.
/// Reads its options from <see cref="IOptionsMonitor{QueueProcessingOptions}"/> so that
/// hot-reloaded configuration is always used on the next start cycle.
/// </summary>
public sealed class QueueRuntime(string queueName, IOptionsMonitor<QueueProcessingOptions> optionsMonitor, IServiceProvider serviceProvider, ProcessorRegistry processorRegistry, ILogger<QueueRuntime> logger)
{
    private readonly string _queueName = queueName;
    private readonly IOptionsMonitor<QueueProcessingOptions> _optionsMonitor = optionsMonitor;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ProcessorRegistry _processorRegistry = processorRegistry;
    private readonly ILogger<QueueRuntime> _logger = logger;

    private Channel<MessageEnvelope>? _channel;
    private AdaptiveReceiver? _receiver;
    private WorkerPool? _workerPool;
    private AutoScaler? _autoScaler;

    public string QueueName => _queueName;

    /// <summary>
    /// Resolves the current options snapshot for this queue.
    /// Called fresh on each <see cref="StartAsync"/> to pick up hot-reloaded values.
    /// </summary>
    public QueueRuntimeOptions GetCurrentOptions()
    {
        var all = _optionsMonitor.CurrentValue;
        return all.Queues.First(q => q.Name.Equals(_queueName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Initializes and starts the queue runtime.
    /// </summary>
    public async Task StartAsync(QueueComponents components, CancellationToken cancellationToken)
    {
        // Always read fresh options so hot reload takes effect
        var options = GetCurrentOptions();

        _logger.LogInformation("Starting queue runtime for {QueueName}", _queueName);

        var registration = _processorRegistry.GetRegistration(_queueName)
            ?? throw new InvalidOperationException($"No processor registered for queue '{_queueName}'");

        // Reset state for potential restart
        _channel = Channel.CreateBounded<MessageEnvelope>(new BoundedChannelOptions(options.ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true
        });

        var deserializer = new MessageDeserializer();

        _workerPool = new WorkerPool(
            queueName: _queueName,
            messageType: registration.MessageType,
            processorType: registration.ProcessorType,
            isBatchProcessor: registration.IsBatchProcessor,
            serviceProvider: _serviceProvider,
            deserializer: deserializer,
            messageDeleter: components.Deleter,
            poisonRouter: components.PoisonRouter,
            retryQueue: components.RetryQueue,
            logger: _serviceProvider.GetRequiredService<ILogger<WorkerPool>>(),
            maxDequeueCount: options.MaxDequeueCount,
            batchSize: options.BatchSize,
            batchFlushTimeoutMs: options.BatchFlushTimeoutMs,
            retryOptions: options.Retry);

        _receiver = new AdaptiveReceiver(
            _queueName,
            components.Receiver,
            _serviceProvider.GetRequiredService<ILogger<AdaptiveReceiver>>(),
            options.BatchSize);

        _workerPool.Start(_channel.Reader, options.MinWorkers, cancellationToken);

        if (options.EnableAdaptiveScaling)
        {
            _autoScaler = new AutoScaler(
                _queueName,
                components.Receiver,
                _workerPool,
                _serviceProvider.GetRequiredService<ILogger<AutoScaler>>(),
                options,
                options.MinWorkers);

            _ = Task.Run(() => _autoScaler.RunAsync(cancellationToken), cancellationToken);
        }

        // Blocks until cancellation
        await _receiver.RunAsync(_channel.Writer, cancellationToken);
    }

    /// <summary>
    /// Gracefully stops the runtime.
    /// </summary>
    public async Task StopAsync()
    {
        _logger.LogInformation("Stopping queue runtime for {QueueName}", _queueName);

        if (_workerPool != null)
            await _workerPool.WaitForCompletionAsync();

        _logger.LogInformation("Queue runtime stopped for {QueueName}", _queueName);
    }
}
