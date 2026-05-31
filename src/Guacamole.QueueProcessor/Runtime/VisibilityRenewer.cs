using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Models;
using Microsoft.Extensions.Logging;

namespace Guacamole.QueueProcessor.Runtime;

/// <summary>
/// Renews message visibility timeout during long-running processing.
/// </summary>
internal sealed class VisibilityRenewer(IVisibilityUpdater visibilityUpdater, ILogger<VisibilityRenewer> logger, int visibilityTimeoutSeconds)
{
    private readonly IVisibilityUpdater _visibilityUpdater = visibilityUpdater;
    private readonly ILogger<VisibilityRenewer> _logger = logger;
    private readonly TimeSpan _renewalInterval = TimeSpan.FromSeconds(visibilityTimeoutSeconds / 2.0);

    /// <summary>
    /// Starts a background renewal task for a message.
    /// The task will automatically stop when the cancellation token is triggered.
    /// </summary>
    public Task StartRenewalAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(_renewalInterval, cancellationToken);

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        await _visibilityUpdater.UpdateVisibilityTimeoutAsync(envelope, cancellationToken);

                        _logger.LogDebug("Renewed visibility timeout for message {MessageId}", envelope.MessageId);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when processing completes
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error renewing visibility timeout for message {MessageId}", envelope.MessageId);
            }
        }, cancellationToken);
    }
}
