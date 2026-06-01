using Azure.Storage.Queues;
using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Guacamole.QueueProcessor.Services.Azure;

/// <summary>
/// Azure-specific implementation of the queue runtime factory.
/// Creates Azure Storage Queue clients and adapters.
/// </summary>
internal sealed class AzureQueueRuntimeFactory : IQueueRuntimeFactory
{
    private readonly IOptionsMonitor<QueueProcessingOptions> _optionsMonitor;
    private readonly ILoggerFactory _loggerFactory;
    private readonly QueueServiceClient _client;
    private readonly Dictionary<string, (QueueClient main, QueueClient deadLetter, QueueClient? retry)> _queueClients = [];

    public AzureQueueRuntimeFactory(IOptionsMonitor<QueueProcessingOptions> optionsMonitor, ILoggerFactory loggerFactory, QueueServiceClient client)
    {
        _optionsMonitor = optionsMonitor;
        _loggerFactory = loggerFactory;
        _client = client;

        // Initialize with current snapshot; re-initialize on each CreateComponents call to pick up changes
        InitializeQueueClients(_optionsMonitor.CurrentValue);
    }

    public QueueComponents CreateComponents(string queueName)
    {
        // Re-read options so hot-reloaded connection strings / queue names are respected
        var options = _optionsMonitor.CurrentValue;
        InitializeQueueClients(options);

        if (!_queueClients.TryGetValue(queueName, out var clients))
            throw new InvalidOperationException($"Queue '{queueName}' not found in configuration");

        var queueOptions = options.Queues.First(q => q.Name.Equals(queueName, StringComparison.OrdinalIgnoreCase));

        var receiver = new AzureMessageReceiver(clients.main, queueOptions.VisibilityTimeoutSeconds);
        var deleter = new AzureMessageDeleter(clients.main);
        var poisonRouter = new AzurePoisonRouter(clients.deadLetter, _loggerFactory.CreateLogger<AzurePoisonRouter>());
        var visibilityUpdater = new AzureVisibilityUpdater(clients.main, queueOptions.VisibilityTimeoutSeconds);

        IRetryQueue? retryQueue = clients.retry is not null
            ? new AzureRetryQueue(clients.retry)
            : null;

        return new QueueComponents
        {
            Receiver = receiver,
            Deleter = deleter,
            PoisonRouter = poisonRouter,
            VisibilityUpdater = visibilityUpdater,
            RetryQueue = retryQueue
        };
    }

    private void InitializeQueueClients(QueueProcessingOptions options)
    {
        foreach (var queueConfig in options.Queues)
        {
            if (_queueClients.ContainsKey(queueConfig.Name))
                continue; // already initialized

            var mainQueueClient = _client.GetQueueClient(queueConfig.Name);
            mainQueueClient.CreateIfNotExists();

            var deadLetterQueueName = queueConfig.DeadLetterQueueName ?? $"{queueConfig.Name}-poison";
            var deadLetterQueueClient = _client.GetQueueClient(deadLetterQueueName);
            deadLetterQueueClient.CreateIfNotExists();

            QueueClient? retryQueueClient = null;
            if (!string.IsNullOrEmpty(queueConfig.RetryQueueName))
            {
                retryQueueClient = _client.GetQueueClient(queueConfig.RetryQueueName);
                retryQueueClient.CreateIfNotExists();
            }

            _queueClients[queueConfig.Name] = (mainQueueClient, deadLetterQueueClient, retryQueueClient);
        }
    }
}
