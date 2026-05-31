using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Runtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Guacamole.QueueProcessor.Services.Core;

/// <summary>
/// Hosted service for a single queue runtime.
/// Each queue gets its own hosted service for isolated lifecycle management.
/// </summary>
public sealed class QueueRuntimeHostedService(QueueRuntime runtime, IQueueRuntimeFactory runtimeFactory, ILogger<QueueRuntimeHostedService> logger) : BackgroundService
{
    private readonly QueueRuntime _runtime = runtime;
    private readonly IQueueRuntimeFactory _runtimeFactory = runtimeFactory;
    private readonly ILogger<QueueRuntimeHostedService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Queue runtime hosted service starting for {QueueName}", _runtime.QueueName);

        try
        {
            // Get provider-specific components from factory
            var (messageReceiver, messageDeleter, poisonRouter) = _runtimeFactory.CreateComponents(_runtime.QueueName);

            // Start the runtime (blocks until cancellation)
            await _runtime.StartAsync(messageReceiver, messageDeleter, poisonRouter, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in queue runtime for {QueueName}", _runtime.QueueName);
            throw;
        }
        finally
        {
            await _runtime.StopAsync();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Queue runtime hosted service stopping for {QueueName}", _runtime.QueueName);

        await base.StopAsync(cancellationToken);
    }
}