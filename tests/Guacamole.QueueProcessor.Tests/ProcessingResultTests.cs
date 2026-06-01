using Guacamole.QueueProcessor.Models;

namespace Guacamole.QueueProcessor.Tests;

public class ProcessingResultTests
{
    [Test]
    public async Task Successful_IsSuccessTrue()
    {
        var result = ProcessingResult.Successful();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ErrorMessage).IsNull();
    }

    [Test]
    public async Task Failed_IsSuccessFalse_WithReason()
    {
        var result = ProcessingResult.Failed("something went wrong", shouldRetry: false);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorMessage).IsEqualTo("something went wrong");
        await Assert.That(result.ShouldRetry).IsFalse();
    }

    [Test]
    public async Task Failed_WithRetry_ShouldRetryTrue()
    {
        var result = ProcessingResult.Failed("transient failure", shouldRetry: true);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ShouldRetry).IsTrue();
    }
}

