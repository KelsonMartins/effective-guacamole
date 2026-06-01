using Guacamole.QueueProcessor.Configuration;
using Guacamole.QueueProcessor.Tests.Fakes;

namespace Guacamole.QueueProcessor.Tests;

public class RetryOptionsTests
{
    [Test]
    public async Task DefaultRetryOptions_HaveReasonableValues()
    {
        var opts = new RetryOptions();

        await Assert.That(opts.MaxAttempts).IsEqualTo(3);
        await Assert.That(opts.InitialDelay).IsEqualTo(TimeSpan.FromMilliseconds(200));
        await Assert.That(opts.MaxDelay).IsEqualTo(TimeSpan.FromSeconds(30));
        await Assert.That(opts.UseJitter).IsTrue();
        await Assert.That(opts.EnableDurableRetry).IsFalse();
    }

    [Test]
    public async Task FakeRetryQueue_SchedulesRetry_RecordsCorrectly()
    {
        var retryQueue = new FakeRetryQueue();
        var envelope = EnvelopeFactory.Create(new { Id = "test" });

        retryQueue.ScheduleRetryAsync(envelope, TimeSpan.FromMinutes(5), CancellationToken.None)
            .GetAwaiter().GetResult();

        await Assert.That(retryQueue.ScheduledRetries).Count().IsEqualTo(1);
        await Assert.That(retryQueue.ScheduledRetries[0].MessageId).IsEqualTo(envelope.MessageId);
        await Assert.That(retryQueue.ScheduledRetries[0].Delay).IsEqualTo(TimeSpan.FromMinutes(5));
    }
}

