using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Guacamole.QueueProcessor.Configuration;
using Guacamole.QueueProcessor.Models;
using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Services.Core;

namespace Guacamole.QueueProcessor.Runtime;

/// <summary>
/// Runtime orchestrator for a single queue.
/// Manages receiver, workers, channel, scaler, and visibility renewal.
/// </summary>
public sealed class QueueRuntime(QueueRuntimeOptions options, IServiceProvider serviceProvider, ProcessorRegistry processorRegistry, ILogger<QueueRuntime> logger)
{
    private readonly QueueRuntimeOptions _options = options;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ProcessorRegistry _processorRegistry = processorRegistry;
    private readonly ILogger<QueueRuntime> _logger = logger;

    private Channel<MessageEnvelope>? _channel;
    private AdaptiveReceiver? _receiver;
    private WorkerPool? _workerPool;
    private AutoScaler? _autoScaler;

    public string QueueName => _options.Name;

    /// <summary>
    /// Initializes and starts the queue runtime.
    /// </summary>
    public async Task StartAsync(IMessageReceiver messageReceiver, IMessageDeleter messageDeleter, IPoisonRouter poisonRouter, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting queue runtime for {QueueName}", _options.Name);

        // Get processor registration
        var registration = _processorRegistry.GetRegistration(_options.Name)
            ?? throw new InvalidOperationException($"No processor registered for queue '{_options.Name}'");

        // Create bounded channel
        _channel = Channel.CreateBounded<MessageEnvelope>(new BoundedChannelOptions(_options.ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true
        });

        // Create components
        var deserializer = new MessageDeserializer();

        _workerPool = new WorkerPool(_options.Name, registration.MessageType, registration.ProcessorType,
                                     _serviceProvider, deserializer, messageDeleter, poisonRouter,
                                     _serviceProvider.GetRequiredService<ILogger<WorkerPool>>(),
                                     _options.MaxDequeueCount);

        _receiver = new AdaptiveReceiver(_options.Name, messageReceiver, _serviceProvider.GetRequiredService<ILogger<AdaptiveReceiver>>(), _options.BatchSize);

        // Start worker pool
        _workerPool.Start(_channel.Reader, _options.MinWorkers, cancellationToken);

        // Start auto-scaler
        if (_options.EnableAdaptiveScaling)
        {
            _autoScaler = new AutoScaler(_options.Name, messageReceiver, _workerPool, _serviceProvider.GetRequiredService<ILogger<AutoScaler>>(), _options, _options.MinWorkers);

            _ = Task.Run(() => _autoScaler.RunAsync(cancellationToken), cancellationToken);
        }

        // Start receiver (runs until cancellation)
        await _receiver.RunAsync(_channel.Writer, cancellationToken);
    }

    /// <summary>
    /// Gracefully stops the runtime.
    /// </summary>
    public async Task StopAsync()
    {
        _logger.LogInformation("Stopping queue runtime for {QueueName}", _options.Name);

        // Channel writer is already completed by receiver
        // Wait for workers to drain the channel
        if (_workerPool != null)
            await _workerPool.WaitForCompletionAsync();

        _logger.LogInformation("Queue runtime stopped for {QueueName}", _options.Name);
    }
}