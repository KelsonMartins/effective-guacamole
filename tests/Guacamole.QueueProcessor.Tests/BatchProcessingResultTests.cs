using Guacamole.QueueProcessor.Models;

namespace Guacamole.QueueProcessor.Tests;

public class BatchProcessingResultTests
{
    [Test]
    public async Task AllSucceeded_CreatesResultWithAllSuccessful()
    {
        var ids = new[] { "m1", "m2", "m3" };

        var result = BatchProcessingResult.AllSucceeded(ids);

        await Assert.That(result.Results).Count().IsEqualTo(3);
        foreach (var id in ids)
        {
            await Assert.That(result.Results.ContainsKey(id)).IsTrue();
            await Assert.That(result.Results[id].Success).IsTrue();
        }
    }

    [Test]
    public async Task AllFailed_CreatesResultWithAllFailed()
    {
        var ids = new[] { "m1", "m2" };

        var result = BatchProcessingResult.AllFailed(ids, "test error");

        await Assert.That(result.Results.Values.All(r => !r.Success)).IsTrue();
    }

    [Test]
    public async Task Mixed_Results_TrackIndividualOutcomes()
    {
        var results = new Dictionary<string, ProcessingResult>
        {
            ["m1"] = ProcessingResult.Successful(),
            ["m2"] = ProcessingResult.Failed("error"),
            ["m3"] = ProcessingResult.Successful()
        };

        var batchResult = new BatchProcessingResult { Results = results };

        await Assert.That(batchResult.Results["m1"].Success).IsTrue();
        await Assert.That(batchResult.Results["m2"].Success).IsFalse();
        await Assert.That(batchResult.Results["m3"].Success).IsTrue();
    }
}

