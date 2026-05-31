using Azure.Storage.Queues;
using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Configuration;
using Microsoft.Extensions.Logging;

namespace Guacamole.QueueProcessor.Services.Azure;

/// <summary>
/// Azure-specific implementation of the queue runtime factory.
/// Creates Azure Storage Queue clients and adapters.
/// </summary>
internal sealed class AzureQueueRuntimeFactory : IQueueRuntimeFactory
{
    private readonly QueueProcessingOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<string, (QueueClient main, QueueClient deadLetter)> _queueClients = [];

    public AzureQueueRuntimeFactory(QueueProcessingOptions options, ILoggerFactory loggerFactory)
    {
        _options = options;
        _loggerFactory = loggerFactory;

        InitializeQueueClients();
    }

    public (IMessageReceiver receiver, IMessageDeleter deleter, IPoisonRouter poisonRouter) CreateComponents(string queueName)
    {
        if (!_queueClients.TryGetValue(queueName, out var clients))
            throw new InvalidOperationException($"Queue '{queueName}' not found in configuration");

        var queueOptions = _options.Queues.First(q => q.Name.Equals(queueName, StringComparison.OrdinalIgnoreCase));

        var receiver = new AzureMessageReceiver(clients.main, queueOptions.VisibilityTimeoutSeconds);
        var deleter = new AzureMessageDeleter(clients.main);
        var poisonRouter = new AzurePoisonRouter(clients.deadLetter, _loggerFactory.CreateLogger<AzurePoisonRouter>());

        return (receiver, deleter, poisonRouter);
    }

    private void InitializeQueueClients()
    {
        if (string.IsNullOrEmpty(_options.ConnectionString))
            throw new InvalidOperationException("Azure Storage connection string is not configured");

        foreach (var queueConfig in _options.Queues)
        {
            // Create main queue client
            var mainQueueClient = new QueueClient(_options.ConnectionString, queueConfig.Name);
            mainQueueClient.CreateIfNotExists();

            // Create dead-letter queue client
            var deadLetterQueueName = queueConfig.DeadLetterQueueName ?? $"{queueConfig.Name}-poison";
            var deadLetterQueueClient = new QueueClient(_options.ConnectionString, deadLetterQueueName);
            deadLetterQueueClient.CreateIfNotExists();

            _queueClients[queueConfig.Name] = (mainQueueClient, deadLetterQueueClient);
        }
    }
}