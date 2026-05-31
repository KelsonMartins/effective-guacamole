using System.Threading.Channels;
using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Models;
using Microsoft.Extensions.Logging;

namespace Guacamole.QueueProcessor.Runtime;

/// <summary>
/// Adaptive receiver that polls messages from a queue provider with dynamic polling intervals.
/// Polling speed adapts based on queue depth to minimize cost and latency.
/// </summary>
internal sealed class AdaptiveReceiver(string queueName, IMessageReceiver messageReceiver, ILogger<AdaptiveReceiver> logger, int batchSize)
{
    private readonly string _queueName = queueName;
    private readonly IMessageReceiver _messageReceiver = messageReceiver;
    private readonly ILogger<AdaptiveReceiver> _logger = logger;
    private readonly int _batchSize = batchSize;

    /// <summary>
    /// Starts the receiver loop that continuously fetches messages and writes to the channel.
    /// </summary>
    public async Task RunAsync(ChannelWriter<MessageEnvelope> channelWriter, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting adaptive receiver for queue {QueueName}", _queueName);

        var consecutiveEmptyPolls = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Fetch messages
                    var messages = await _messageReceiver.ReceiveMessagesAsync(_batchSize, cancellationToken);

                    if (messages.Count == 0)
                    {
                        consecutiveEmptyPolls++;
                        await DelayForEmptyPoll(consecutiveEmptyPolls, cancellationToken);
                        continue;
                    }

                    // Reset empty poll counter
                    consecutiveEmptyPolls = 0;

                    // Write messages to channel
                    foreach (var message in messages)
                        await channelWriter.WriteAsync(message, cancellationToken);

                    // Adaptive polling based on queue depth
                    var pollDelay = CalculatePollDelay(messages.Count);
                    if (pollDelay > TimeSpan.Zero)
                        await Task.Delay(pollDelay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error receiving messages from queue {QueueName}", _queueName);

                    // Back off on errors
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
        }
        finally
        {
            channelWriter.Complete();
            _logger.LogInformation("Adaptive receiver stopped for queue {QueueName}", _queueName);
        }
    }

    // Adaptive polling intervals based on queue depth
    private static TimeSpan CalculatePollDelay(int messageCount)
    => messageCount switch
    {
        >= 1000 => TimeSpan.FromMilliseconds(50),
        >= 100 => TimeSpan.FromMilliseconds(100),
        >= 10 => TimeSpan.FromMilliseconds(250),
        > 0 => TimeSpan.FromMilliseconds(500),
        _ => TimeSpan.FromSeconds(2)
    };

    private static async Task DelayForEmptyPoll(int consecutiveEmptyPolls, CancellationToken cancellationToken)
    {
        // Exponential backoff for empty polls, capped at 30 seconds
        var delay = consecutiveEmptyPolls switch
        {
            1 => TimeSpan.FromSeconds(1),
            2 => TimeSpan.FromSeconds(2),
            3 => TimeSpan.FromSeconds(5),
            4 => TimeSpan.FromSeconds(10),
            _ => TimeSpan.FromSeconds(30)
        };

        await Task.Delay(delay, cancellationToken);
    }
}