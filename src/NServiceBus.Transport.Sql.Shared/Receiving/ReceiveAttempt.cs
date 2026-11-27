namespace NServiceBus.Transport.Sql.Shared
{
    using System;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A single receive handed to a <see cref="ProcessStrategy"/> by the receive loop.
    /// Owns the anchored query and reports back to the loop (the receive latch and stopping an empty batch).
    /// </summary>
    sealed class ReceiveAttempt(TableBasedQueue inputQueue, ReceiveCursor receiveCursor, ReceiveCountdownEvent.Signaler receiveCountdownEventSignaler, CancellationTokenSource stopBatchCancellationTokenSource)
    {
        public async Task<MessageReadResult> Receive(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
        {
            if (receiveStarted)
            {
                throw new InvalidOperationException("A receive attempt can only receive once.");
            }
            receiveStarted = true;

            var anchor = receiveCursor.GetAnchor();
            var receiveResult = await inputQueue.TryReceive(connection, transaction, anchor.SeekAfter, cancellationToken).ConfigureAwait(false);

            if (!receiveResult.IsEmpty)
            {
                receivedRowVersion = receiveResult.RowVersion;
            }

            receiveCursor.Received(anchor, receivedRowVersion);

            receiveCountdownEventSignaler.Signal();

            if (receiveResult.IsEmpty)
            {
                stopBatchCancellationTokenSource.Cancel();
            }

            return receiveResult;
        }

        /// <summary>
        /// Rescans from the received row if it rolled back. The anchor already moved past it on receive.
        /// </summary>
        public void Settle(ProcessOutcome outcome)
        {
            if (outcome == ProcessOutcome.RolledBack && receivedRowVersion is { } rowVersion)
            {
                receiveCursor.RescanFrom(rowVersion);
            }
        }

        bool receiveStarted;
        long? receivedRowVersion;
    }
}
