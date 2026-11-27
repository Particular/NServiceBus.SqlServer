namespace NServiceBus.Transport.Sql.Shared
{
    using System.Threading;

    /// <summary>
    /// Where in the queue the next receive seeks from, shared between the receive loop and the concurrent receives it starts.
    /// </summary>
    /// <remarks>
    /// Messages are received in sequence number order, oldest first. Without anchoring, each attempt
    /// to pop the next message off the head of the queue requires wading past rows deleted by
    /// open transactions or ghost records. As the number of competing receivers increases, the
    /// number of deleted records and ghost records that must be skipped increases, which
    /// increases database load.
    /// <para>
    /// The anchor remembers how far into the queue we have received, so receive queries can seek
    /// past the contended head, reducing database load. Anchoring is on unless the
    /// <c>Receiving.DisableAnchoring</c> AppContext switch is set.
    /// </para>
    /// <para>
    /// Two anchors are held:
    /// <list type="bullet">
    /// <item>The highest received row version, or just before the lowest row a peek found if that is
    /// higher. It only moves forward.</item>
    /// <item>The sweep anchor: set when a row is found below the highest received row version, which
    /// receives would otherwise skip.</item>
    /// </list>
    /// Rows can be missed because a transaction rolled back, or because messages finished
    /// sending in a different order to the one they started in, so an earlier row shows up late.
    /// The sweep anchor is set when:
    /// <list type="bullet">
    /// <item>a local receive rolls back;</item>
    /// <item>the peek query finds a row below the highest received row version;</item>
    /// <item><see cref="QueuePeekerOptions.HeadSweepInterval"/> elapses, which sweeps from the
    /// start of the queue to catch anything the peek could not see.</item>
    /// </list>
    /// While set, receives seek from the sweep anchor instead, hopping from one missed row to the
    /// next, until a receive finds nothing below the highest received row version and the sweep ends.
    /// </para>
    /// <para>
    /// Every sweep request bumps a generation, even when the sweep anchor does not move, and the anchor a receive
    /// reads carries it. A sweep receive that started under an older generation may have skipped the requested row,
    /// so it leaves the sweep as it is. When the generation is unchanged, a sweep that is set now was already set when
    /// the anchor was read, so the receive was a sweep receive; that is how the two kinds of receive are told apart.
    /// </para>
    /// </remarks>
    class ReceiveCursor(bool anchoringEnabled)
    {
        public ReceiveAnchor GetAnchor()
        {
            lock (stateLock)
            {
                return new ReceiveAnchor(sweepAnchor != NoSweep ? sweepAnchor : highestReceivedRowVersion, sweepGeneration);
            }
        }

        public long GetHighestReceivedRowVersion()
        {
            lock (stateLock)
            {
                return highestReceivedRowVersion;
            }
        }

        public void ApplyPeekResult(long lowestRowVersion)
        {
            var seekAfter = lowestRowVersion - 1;
            lock (stateLock)
            {
                if (seekAfter < highestReceivedRowVersion)
                {
                    RequestSweep(seekAfter);
                }
                else
                {
                    AdvanceHighestReceived(seekAfter);
                }
            }
        }

        /// <summary>
        /// Sweeps from the start of the queue, finding rows stranded anywhere below the highest received row version
        /// </summary>
        public void SweepFromHead()
        {
            lock (stateLock)
            {
                if (highestReceivedRowVersion > 0)
                {
                    RequestSweep(0);
                }
            }
        }

        /// <summary>
        /// Sweeps from just before a row that rolled back, so we rescan a failed row
        /// </summary>
        public void RescanFrom(long rowVersion)
        {
            var seekAfter = rowVersion - 1;
            lock (stateLock)
            {
                if (seekAfter < highestReceivedRowVersion)
                {
                    RequestSweep(seekAfter);
                }
            }
        }

        /// <summary>
        /// Moves the anchor as soon as a receive returns, before the message is processed; the row is locked,
        /// so no other receive can take it, and a rollback calls <see cref="RescanFrom"/>.
        /// A received row above the highest received row version becomes the new highest. A sweep receive that
        /// found a row below it carries on from that row; otherwise the sweep is over. A receive that read the anchor
        /// under an older generation leaves the sweep alone, because a lower sweep was requested while it ran.
        /// </summary>
        public void Received(ReceiveAnchor usedAnchor, long? receivedRowVersion)
        {
            lock (stateLock)
            {
                var sweepReceive = sweepAnchor != NoSweep && usedAnchor.Generation == sweepGeneration;
                if (sweepReceive)
                {
                    if (receivedRowVersion < highestReceivedRowVersion)
                    {
                        MoveSweepPast(receivedRowVersion.Value);
                        return;
                    }

                    sweepAnchor = NoSweep;
                }

                if (receivedRowVersion is { } rowVersion)
                {
                    AdvanceHighestReceived(rowVersion);
                }
            }
        }

        void RequestSweep(long seekAfter)
        {
            if (seekAfter < sweepAnchor)
            {
                sweepAnchor = seekAfter;
            }

            sweepGeneration++;
        }

        void MoveSweepPast(long rowVersion)
        {
            if (sweepAnchor != NoSweep && rowVersion > sweepAnchor)
            {
                sweepAnchor = rowVersion;
            }
        }

        void AdvanceHighestReceived(long rowVersion)
        {
            if (anchoringEnabled && rowVersion > highestReceivedRowVersion)
            {
                highestReceivedRowVersion = rowVersion;
            }
        }

        const long NoSweep = long.MaxValue;

        readonly Lock stateLock = new();
        long highestReceivedRowVersion;
        long sweepAnchor = NoSweep;
        long sweepGeneration;
    }

    /// <summary>
    /// Where a receive seeks from, and the sweep generation the cursor was at when it was read.
    /// </summary>
    readonly record struct ReceiveAnchor(long SeekAfter, long Generation = 0);
}
