using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Models;

namespace Guacamole.QueueProcessor.Tests.Fakes;

public sealed class FakeMessageDeleter : IMessageDeleter
{
    public List<string> DeletedMessageIds { get; } = [];

    public Task DeleteMessageAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        DeletedMessageIds.Add(envelope.MessageId);
        return Task.CompletedTask;
    }
}

public sealed class FakePoisonRouter : IPoisonRouter
{
    public List<(string MessageId, string Reason)> RoutedMessages { get; } = [];

    public Task RouteToDeadLetterAsync(MessageEnvelope envelope, string reason, CancellationToken cancellationToken)
    {
        RoutedMessages.Add((envelope.MessageId, reason));
        return Task.CompletedTask;
    }
}

public sealed class FakeVisibilityUpdater : IVisibilityUpdater
{
    public List<string> RenewedMessageIds { get; } = [];

    public Task UpdateVisibilityTimeoutAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        RenewedMessageIds.Add(envelope.MessageId);
        return Task.CompletedTask;
    }
}

public sealed class FakeRetryQueue : IRetryQueue
{
    public List<(string MessageId, TimeSpan Delay)> ScheduledRetries { get; } = [];

    public Task ScheduleRetryAsync(MessageEnvelope envelope, TimeSpan delay, CancellationToken cancellationToken)
    {
        ScheduledRetries.Add((envelope.MessageId, delay));
        return Task.CompletedTask;
    }
}
