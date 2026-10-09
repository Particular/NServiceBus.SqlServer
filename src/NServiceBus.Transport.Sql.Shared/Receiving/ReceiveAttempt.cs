namespace NServiceBus.Transport.Sql.Shared
{
    using System;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A single receive handed to a <see cref="ProcessStrategy"/> by the receive loop.
    /// Owns the anchored query and reports back to the loop (the receive latch, the batch backoff and stopping an empty batch).
    /// </summary>
    sealed class ReceiveAttempt(TableBasedQueue inputQueue, ReceiveState receiveState, ReceiveCountdownEvent.Signaler receiveCountdownEventSignaler, CancellationTokenSource stopBatchCancellationTokenSource)
    {
        public async Task<MessageReadResult> Receive(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
        {
            if (receiveStarted)
            {
                throw new InvalidOperationException("A receive attempt can only receive once.");
            }
            receiveStarted = true;

            var anchor = receiveState.GetAnchor();
            var receiveResult = await inputQueue.TryReceive(connection, transaction, anchor.Anchor, cancellationToken).ConfigureAwait(false);

            if (receiveResult != MessageReadResult.NoMessage)
            {
                receivedRowVersion = receiveResult.RowVersion;
                receiveState.MarkReceived();
            }

            receiveState.AdvanceOrEndSweep(anchor, receivedRowVersion);

            receiveCountdownEventSignaler.Signal();

            if (receiveResult == MessageReadResult.NoMessage)
            {
                stopBatchCancellationTokenSource.Cancel();
            }

            return receiveResult;
        }

        /// <summary>
        /// Moves the anchor according to what happened to the received row.
        /// </summary>
        public void Settle(ProcessOutcome outcome)
        {
            if (receivedRowVersion is not { } rowVersion)
            {
                // Nothing received - no matter the reported outcome, do nothing
                return;
            }

            switch (outcome)
            {
                case ProcessOutcome.Committed:
                    receiveState.AdvanceAnchor(rowVersion);
                    break;
                case ProcessOutcome.RolledBack:
                    receiveState.RescanFrom(rowVersion);
                    break;
                case ProcessOutcome.NoMessage:
                default:
                    break;
            }
        }

        bool receiveStarted;
        long? receivedRowVersion;
    }
}
