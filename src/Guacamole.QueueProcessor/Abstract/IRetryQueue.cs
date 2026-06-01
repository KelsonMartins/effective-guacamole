using Guacamole.QueueProcessor.Models;

namespace Guacamole.QueueProcessor.Abstract;

/// <summary>
/// Schedules a failed message for durable retry after a delay.
/// Used when the in-process resilience pipeline exhausts its attempts
/// and the message should be requeued rather than dead-lettered.
/// </summary>
public interface IRetryQueue
{
    /// <summary>
    /// Schedules the message to become visible again after the specified delay.
    /// </summary>
    /// <param name="envelope">The original message envelope</param>
    /// <param name="delay">How long to wait before the message is requeued</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ScheduleRetryAsync(MessageEnvelope envelope, TimeSpan delay, CancellationToken cancellationToken);
}
