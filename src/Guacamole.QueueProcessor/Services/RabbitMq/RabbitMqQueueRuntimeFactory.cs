using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Guacamole.QueueProcessor.Services.RabbitMq;

/// <summary>
/// RabbitMQ implementation of the queue runtime factory.
/// Creates one <see cref="IChannel"/> per queue for channel-level isolation.
/// </summary>
internal sealed class RabbitMqQueueRuntimeFactory : IQueueRuntimeFactory, IAsyncDisposable
{
    private readonly IOptionsMonitor<QueueProcessingOptions> _optionsMonitor;
    private readonly ILoggerFactory _loggerFactory;
    private IConnection? _connection;
    private readonly Dictionary<string, IChannel> _channels = [];

    public RabbitMqQueueRuntimeFactory(IOptionsMonitor<QueueProcessingOptions> optionsMonitor, ILoggerFactory loggerFactory)
    {
        _optionsMonitor = optionsMonitor;
        _loggerFactory = loggerFactory;
    }

    public QueueComponents CreateComponents(string queueName)
    {
        var options = _optionsMonitor.CurrentValue;

        if (string.IsNullOrEmpty(options.RabbitMqUri))
            throw new InvalidOperationException("RabbitMqUri is not configured");

        // Lazy-create connection
        if (_connection is null || !_connection.IsOpen)
        {
            var factory = new ConnectionFactory { Uri = new Uri(options.RabbitMqUri) };
            _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        }

        // One channel per queue for isolation; recreate if closed
        if (!_channels.TryGetValue(queueName, out var channel) || channel.IsClosed)
        {
            channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();
            _channels[queueName] = channel;
        }

        return new QueueComponents
        {
            Receiver = new RabbitMqMessageReceiver(channel, queueName),
            Deleter = new RabbitMqMessageDeleter(),
            PoisonRouter = new RabbitMqPoisonRouter(_loggerFactory.CreateLogger<RabbitMqPoisonRouter>()),
            VisibilityUpdater = new RabbitMqVisibilityUpdater(),
            RetryQueue = null
        };
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var channel in _channels.Values)
            await channel.DisposeAsync();

        _channels.Clear();

        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
