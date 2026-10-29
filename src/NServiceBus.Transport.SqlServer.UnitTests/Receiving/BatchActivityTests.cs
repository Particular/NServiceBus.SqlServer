namespace NServiceBus.Transport.SqlServer.UnitTests.Receiving;

using NServiceBus.Transport.Sql.Shared;
using NUnit.Framework;

public class BatchActivityTests
{
    [Test]
    public void First_batch_does_not_back_off()
    {
        var batchActivity = new BatchActivity();

        Assert.That(batchActivity.BeginNextBatch(), Is.EqualTo(LastBatchResult.PreviousBatchHadResultsOrNoPreviousBatch));
    }

    [Test]
    public void Reports_whether_the_previous_batch_received_anything()
    {
        var batchActivity = new BatchActivity();
        _ = batchActivity.BeginNextBatch();

        Assert.That(batchActivity.BeginNextBatch(), Is.EqualTo(LastBatchResult.LastBatchWasEmpty), "nothing received in the previous batch");

        batchActivity.MarkReceived();

        Assert.Multiple(() =>
        {
            Assert.That(batchActivity.BeginNextBatch(), Is.EqualTo(LastBatchResult.PreviousBatchHadResultsOrNoPreviousBatch), "previous batch received a message");
            Assert.That(batchActivity.BeginNextBatch(), Is.EqualTo(LastBatchResult.LastBatchWasEmpty), "starting a batch clears the flag");
        });
    }
}
