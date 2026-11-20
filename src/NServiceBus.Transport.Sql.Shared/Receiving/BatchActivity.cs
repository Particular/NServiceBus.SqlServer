namespace NServiceBus.Transport.Sql.Shared
{
    using System.Threading;

    /// <summary>
    /// Tracks whether a receive batch received any messages, so the receive loop can back off after an empty one.
    /// </summary>
    class BatchActivity
    {
        /// <summary>
        /// Starts a new "receive" batch and returns whether the previous batch received any messages
        /// </summary>
        public LastBatchResult BeginNextBatch() => Interlocked.Exchange(ref hasReceivedAnyMessagesInBatch, false)
            ? LastBatchResult.PreviousBatchHadResultsOrNoPreviousBatch
            : LastBatchResult.LastBatchWasEmpty;

        public void MarkReceived() => Interlocked.Exchange(ref hasReceivedAnyMessagesInBatch, true);

        // starts set so the first batch does not wait for the peek delay
        bool hasReceivedAnyMessagesInBatch = true;
    }

    enum LastBatchResult { PreviousBatchHadResultsOrNoPreviousBatch, LastBatchWasEmpty }
}
