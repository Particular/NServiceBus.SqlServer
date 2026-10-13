namespace NServiceBus.Transport.Sql.Shared
{
    using System.Threading;

    /// <summary>
    /// Per-receiver state shared between the receive loop and the concurrent receives it starts.
    /// </summary>
    /// <remarks>
    /// Messages are received in sequence number order, oldest first. Without anchoring, each attempt
    /// to pop the next message off the head of the queue requires wading past rows deleted by
    /// open transactions or ghost records. As the number of competing receivers increases, the
    /// number of deleted records and ghost records that must be skipped increases, which
    /// increases database load.
    /// <para>
    /// The anchor remembers how far into the queue we have received, so receive queries can seek
    /// past the contended head, reducing database load. Anchoring is only enabled when
    /// <see cref="QueuePeekerOptions.HeadSweepInterval"/> is set.
    /// </para>
    /// <para>
    /// Two anchors are held:
    /// <list type="bullet">
    /// <item>The high-water anchor: the highest sequence number received. It only moves forward.</item>
    /// <item>The sweep anchor: set when an earlier row is found that the high-water anchor would
    /// skip.</item>
    /// </list>
    /// Rows can be missed because a transaction rolled back, or because messages finished
    /// sending in a different order to the one they started in, so an earlier row shows up late.
    /// The sweep anchor is set when:
    /// <list type="bullet">
    /// <item>a local receive rolls back;</item>
    /// <item>the peek query finds an earlier row than the high-water anchor;</item>
    /// <item><see cref="QueuePeekerOptions.HeadSweepInterval"/> elapses, which sweeps from the
    /// start of the queue to catch anything the peek could not see.</item>
    /// </list>
    /// While set, receives seek from the sweep anchor instead, hopping from one missed row to the
    /// next, until a receive finds nothing behind the high-water anchor and the sweep ends.
    /// </para>
    /// </remarks>
    class ReceiveState(bool anchoringEnabled)
    {
        public ReceiveAnchor GetAnchor()
        {
            var sweep = Interlocked.Read(ref sweepAnchor);
            return sweep != NoSweep
                ? ReceiveAnchor.Sweep(sweep)
                : ReceiveAnchor.HighWater(Interlocked.Read(ref highWaterAnchor));
        }

        public void ApplyPeekResult(long lowestRowVersion)
        {
            var seekFrom = lowestRowVersion - 1;
            if (seekFrom < Interlocked.Read(ref highWaterAnchor))
            {
                StartOrLowerSweep(seekFrom);
            }
            else
            {
                AdvanceAnchor(seekFrom);
            }
        }

        /// <summary>
        /// Sweeps from the start of the queue, finding rows stranded anywhere behind the high-water anchor
        /// </summary>
        public void SweepFromHead()
        {
            if (Interlocked.Read(ref highWaterAnchor) > 0)
            {
                StartOrLowerSweep(0);
            }
        }

        public void AdvanceAnchor(long rowVersion)
        {
            if (!anchoringEnabled)
            {
                return;
            }

            var current = Interlocked.Read(ref highWaterAnchor);
            while (rowVersion > current)
            {
                var witnessed = Interlocked.CompareExchange(ref highWaterAnchor, rowVersion, current);
                if (witnessed == current)
                {
                    break;
                }

                current = witnessed;
            }
        }

        /// <summary>
        /// Sweeps from just before a row that rolled back, so we rescan a failed row
        /// </summary>
        public void RescanFrom(long rowVersion)
        {
            var seekFrom = rowVersion - 1;
            if (seekFrom < Interlocked.Read(ref highWaterAnchor))
            {
                StartOrLowerSweep(seekFrom);
            }
        }

        /// <summary>
        /// After a sweep receive: if it found a message left behind the high-water anchor, the sweep carries on
        /// from that message; if it found nothing left behind, the sweep is over.
        /// </summary>
        public void AdvanceOrEndSweep(ReceiveAnchor usedAnchor, long? receivedRowVersion)
        {
            if (usedAnchor.Kind == AnchorKind.Sweep)
            {
                // don't end a sweep that a rollback has moved further back since this receive started
                if (receivedRowVersion is { } rowVersion && rowVersion < Interlocked.Read(ref highWaterAnchor))
                {
                    MoveSweepPast(rowVersion);
                }
                else
                {
                    _ = Interlocked.CompareExchange(ref sweepAnchor, NoSweep, usedAnchor.Anchor);
                }
            }
        }

        void StartOrLowerSweep(long seekFrom)
        {
            var current = Interlocked.Read(ref sweepAnchor);
            while (seekFrom < current)
            {
                var witnessed = Interlocked.CompareExchange(ref sweepAnchor, seekFrom, current);
                if (witnessed == current)
                {
                    break;
                }

                current = witnessed;
            }
        }

        void MoveSweepPast(long rowVersion)
        {
            var current = Interlocked.Read(ref sweepAnchor);
            while (current != NoSweep && rowVersion > current)
            {
                var witnessed = Interlocked.CompareExchange(ref sweepAnchor, rowVersion, current);
                if (witnessed == current)
                {
                    break;
                }

                current = witnessed;
            }
        }

        /// <summary>
        /// Starts a new "receive" batch and returns true if the previous batch received anything
        /// </summary>
        public bool BeginBatch() => Interlocked.Exchange(ref receivedInBatch, 0) == 1;
        public void MarkReceived() => Interlocked.Exchange(ref receivedInBatch, 1);

        const long NoSweep = long.MaxValue;

        long highWaterAnchor;
        long sweepAnchor = NoSweep;
        // starts set so the first batch does not wait for the peek delay
        int receivedInBatch = 1;
    }

    enum AnchorKind { HighWater, Sweep }
    readonly record struct ReceiveAnchor(long Anchor, AnchorKind Kind)
    {
        public static ReceiveAnchor HighWater(long anchor) => new(anchor, AnchorKind.HighWater);
        public static ReceiveAnchor Sweep(long anchor) => new(anchor, AnchorKind.Sweep);
    }
}
