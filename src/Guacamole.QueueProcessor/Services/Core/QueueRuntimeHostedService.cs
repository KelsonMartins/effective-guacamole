using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Configuration;
using Guacamole.QueueProcessor.Runtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Guacamole.QueueProcessor.Services.Core;

/// <summary>
/// Hosted service for a single queue runtime.
/// Listens to <see cref="IOptionsMonitor{QueueProcessingOptions}"/> and automatically
/// restarts the runtime when this queue's configuration changes (hot reload).
/// </summary>
public sealed class QueueRuntimeHostedService(QueueRuntime runtime, IQueueRuntimeFactory runtimeFactory, IOptionsMonitor<QueueProcessingOptions> optionsMonitor, ILogger<QueueRuntimeHostedService> logger) : BackgroundService
{
    private readonly QueueRuntime _runtime = runtime;
    private readonly IQueueRuntimeFactory _runtimeFactory = runtimeFactory;
    private readonly IOptionsMonitor<QueueProcessingOptions> _optionsMonitor = optionsMonitor;
    private readonly ILogger<QueueRuntimeHostedService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Queue runtime hosted service starting for {QueueName}", _runtime.QueueName);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Snapshot the current options for this queue so we can detect changes
            var currentOptions = _runtime.GetCurrentOptions();

            using var runtimeCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

            // Subscribe to config changes for this run cycle
            using var changeRegistration = _optionsMonitor.OnChange(newOptions =>
            {
                var updated = newOptions.Queues
                    .FirstOrDefault(q => q.Name.Equals(_runtime.QueueName, StringComparison.OrdinalIgnoreCase));

                if (updated is null)
                    return;

                // Simple structural equality check via JSON round-trip is too heavy;
                // compare the fields that meaningfully affect runtime behaviour.
                if (HasSignificantChange(currentOptions, updated))
                {
                    _logger.LogInformation("Configuration changed for queue {QueueName} — scheduling hot reload", _runtime.QueueName);
                    runtimeCts.Cancel();
                }
            });

            try
            {
                var components = _runtimeFactory.CreateComponents(_runtime.QueueName);
                await _runtime.StartAsync(components, runtimeCts.Token);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                // Hot reload triggered — loop and restart with fresh options
                _logger.LogInformation("Restarting queue runtime {QueueName} after config change", _runtime.QueueName);
                await _runtime.StopAsync();
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in queue runtime for {QueueName}", _runtime.QueueName);
                throw;
            }
        }

        await _runtime.StopAsync();
        _logger.LogInformation("Queue runtime hosted service stopped for {QueueName}", _runtime.QueueName);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Queue runtime hosted service stopping for {QueueName}", _runtime.QueueName);
        await base.StopAsync(cancellationToken);
    }

    private static bool HasSignificantChange(QueueRuntimeOptions current, QueueRuntimeOptions updated)
        => current.MinWorkers != updated.MinWorkers
        || current.MaxWorkers != updated.MaxWorkers
        || current.BatchSize != updated.BatchSize
        || current.ChannelCapacity != updated.ChannelCapacity
        || current.VisibilityTimeoutSeconds != updated.VisibilityTimeoutSeconds
        || current.MaxDequeueCount != updated.MaxDequeueCount
        || current.EnableAdaptiveScaling != updated.EnableAdaptiveScaling
        || current.Retry.MaxAttempts != updated.Retry.MaxAttempts
        || current.Retry.InitialDelay != updated.Retry.InitialDelay
        || current.Retry.EnableDurableRetry != updated.Retry.EnableDurableRetry;
}
