using Azure.Messaging.ServiceBus;
using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Guacamole.QueueProcessor.Services.ServiceBus;

/// <summary>
/// Azure Service Bus implementation of the queue runtime factory.
/// Creates a <see cref="ServiceBusReceiver"/> per queue and wraps it
/// in the provider-agnostic component interfaces.
/// </summary>
internal sealed class ServiceBusQueueRuntimeFactory : IQueueRuntimeFactory, IAsyncDisposable
{
    private readonly IOptionsMonitor<QueueProcessingOptions> _optionsMonitor;
    private readonly ILoggerFactory _loggerFactory;
    private ServiceBusClient? _client;
    private readonly Dictionary<string, ServiceBusReceiver> _receivers = [];

    public ServiceBusQueueRuntimeFactory(IOptionsMonitor<QueueProcessingOptions> optionsMonitor, ILoggerFactory loggerFactory)
    {
        _optionsMonitor = optionsMonitor;
        _loggerFactory = loggerFactory;
    }

    public QueueComponents CreateComponents(string queueName)
    {
        var options = _optionsMonitor.CurrentValue;

        if (string.IsNullOrEmpty(options.ServiceBusConnectionString))
            throw new InvalidOperationException("ServiceBusConnectionString is not configured");

        // Recreate client if connection string changed
        if (_client is null)
            _client = new ServiceBusClient(options.ServiceBusConnectionString);

        // Reuse receiver if already created; create fresh one on each hot-reload cycle
        if (!_receivers.TryGetValue(queueName, out var receiver))
        {
            receiver = _client.CreateReceiver(queueName);
            _receivers[queueName] = receiver;
        }

        return new QueueComponents
        {
            Receiver = new ServiceBusMessageReceiver(receiver),
            Deleter = new ServiceBusMessageDeleter(),
            PoisonRouter = new ServiceBusPoisonRouter(_loggerFactory.CreateLogger<ServiceBusPoisonRouter>()),
            VisibilityUpdater = new ServiceBusVisibilityUpdater(),
            RetryQueue = null // Service Bus has native retry/DLQ support; durable retry not needed
        };
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var receiver in _receivers.Values)
            await receiver.DisposeAsync();

        _receivers.Clear();

        if (_client is not null)
            await _client.DisposeAsync();
    }
}
