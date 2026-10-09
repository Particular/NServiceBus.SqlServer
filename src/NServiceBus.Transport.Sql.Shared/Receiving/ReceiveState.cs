namespace NServiceBus.Transport.Sql.Shared
{
    using System.Threading;

    /// <summary>
    /// Per-receiver state shared between the receive loop and the concurrent receives it starts.
    /// </summary>
    /// <remarks>
    /// The anchor lets receive queries seek past the contended head of the queue index. The head
    /// accumulates other receivers' in-flight (locked, delete-pending) rows and ghost records of
    /// recently deleted rows; scanning over it makes every receive more expensive as more
    /// competing instances are added.
    /// <para>
    /// Rows can become visible behind the anchor: rolled back here or on another instance, or
    /// committed out of row version order. When that is detected (a local rollback, or a peek
    /// whose lowest row is behind the anchor) a sweep starts just before them. Receives seek from
    /// the sweep instead of the anchor, hopping from one stranded row to the next, until a receive
    /// reaches the anchor again; commits keep advancing the anchor meanwhile without affecting it.
    /// </para>
    /// <para>
    /// The sweep should also be triggered periodically, as out-of-order commits can also leave gaps
    /// in message processing.
    /// </para>
    /// </remarks>
    class ReceiveState(bool anchoringEnabled)
    {
        public ReceiveAnchor GetAnchor()
        {
            var sweep = Interlocked.Read(ref sweepAnchor);
            return sweep != NoSweep
                ? ReceiveAnchor.Sweep(sweep)
                : ReceiveAnchor.Fast(Interlocked.Read(ref anchor));
        }

        public void ApplyPeekResult(long lowestRowVersion)
        {
            var seekFrom = lowestRowVersion - 1;
            if (seekFrom < Interlocked.Read(ref anchor))
            {
                StartOrLowerSweep(seekFrom);
            }
            else
            {
                AdvanceAnchor(seekFrom);
            }
        }

        /// <summary>
        /// Sweeps from the start of the queue, finding rows stranded anywhere behind the anchor
        /// </summary>
        public void SweepFromHead()
        {
            if (Interlocked.Read(ref anchor) > 0)
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

            var current = Interlocked.Read(ref anchor);
            while (rowVersion > current)
            {
                var witnessed = Interlocked.CompareExchange(ref anchor, rowVersion, current);
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
            if (seekFrom < Interlocked.Read(ref anchor))
            {
                StartOrLowerSweep(seekFrom);
            }
        }

        /// <summary>
        /// After a sweep receive: if it found a message left behind the anchor, the sweep carries on
        /// from that message; if it found nothing left behind, the sweep is over.
        /// </summary>
        public void AdvanceOrEndSweep(ReceiveAnchor usedAnchor, long? receivedRowVersion)
        {
            if (usedAnchor.Kind == AnchorKind.Fast)
            {
                return;
            }

            // don't end a sweep that a rollback has moved further back since this receive started
            if (receivedRowVersion is { } rowVersion && rowVersion < Interlocked.Read(ref anchor))
            {
                MoveSweepPast(rowVersion);
            }
            else
            {
                _ = Interlocked.CompareExchange(ref sweepAnchor, NoSweep, usedAnchor.Anchor);
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

        long anchor;
        long sweepAnchor = NoSweep;
        // starts set so the first batch does not wait for the peek delay
        int receivedInBatch = 1;
    }

    enum AnchorKind { Fast, Sweep }
    readonly record struct ReceiveAnchor(long Anchor, AnchorKind Kind)
    {
        public static ReceiveAnchor Fast(long anchor) => new(anchor, AnchorKind.Fast);
        public static ReceiveAnchor Sweep(long anchor) => new(anchor, AnchorKind.Sweep);
    }
}
