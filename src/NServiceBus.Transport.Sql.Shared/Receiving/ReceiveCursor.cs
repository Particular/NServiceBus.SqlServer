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
    /// </remarks>
    class ReceiveCursor(bool anchoringEnabled)
    {
        public ReceiveAnchor GetAnchor()
        {
            var sweep = Interlocked.Read(ref sweepAnchor);
            return sweep != NoSweep
                ? ReceiveAnchor.Sweep(sweep)
                : ReceiveAnchor.HighestReceived(Interlocked.Read(ref highestReceivedRowVersion));
        }

        public long GetHighestReceivedRowVersion() => Interlocked.Read(ref highestReceivedRowVersion);

        public void ApplyPeekResult(long lowestRowVersion)
        {
            var seekAfter = lowestRowVersion - 1;
            if (seekAfter < Interlocked.Read(ref highestReceivedRowVersion))
            {
                StartOrLowerSweep(seekAfter);
            }
            else
            {
                AdvanceHighestReceived(seekAfter);
            }
        }

        /// <summary>
        /// Sweeps from the start of the queue, finding rows stranded anywhere below the highest received row version
        /// </summary>
        public void SweepFromHead()
        {
            if (Interlocked.Read(ref highestReceivedRowVersion) > 0)
            {
                StartOrLowerSweep(0);
            }
        }

        void AdvanceHighestReceived(long rowVersion)
        {
            if (!anchoringEnabled)
            {
                return;
            }

            var current = Interlocked.Read(ref highestReceivedRowVersion);
            while (rowVersion > current)
            {
                var witnessed = Interlocked.CompareExchange(ref highestReceivedRowVersion, rowVersion, current);
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
            var seekAfter = rowVersion - 1;
            if (seekAfter < Interlocked.Read(ref highestReceivedRowVersion))
            {
                StartOrLowerSweep(seekAfter);
            }
        }

        /// <summary>
        /// Moves the anchor as soon as a receive returns, before the message is processed; the row is locked,
        /// so no other receive can take it, and a rollback calls <see cref="RescanFrom"/>.
        /// A received row above the highest received row version becomes the new highest. A sweep receive that
        /// found a row below it carries on from that row; otherwise the sweep is over.
        /// </summary>
        public void Received(ReceiveAnchor usedAnchor, long? receivedRowVersion)
        {
            if (usedAnchor.Kind == AnchorKind.Sweep)
            {
                if (receivedRowVersion is { } && receivedRowVersion < Interlocked.Read(ref highestReceivedRowVersion))
                {
                    MoveSweepPast(usedAnchor.SeekAfter, receivedRowVersion.Value);
                    return;
                }

                // don't end a sweep that a rollback has moved further back since this receive started
                _ = Interlocked.CompareExchange(ref sweepAnchor, NoSweep, usedAnchor.SeekAfter);
            }

            if (receivedRowVersion is { } rowVersion)
            {
                AdvanceHighestReceived(rowVersion);
            }

        }

        void StartOrLowerSweep(long seekAfter)
        {
            var current = Interlocked.Read(ref sweepAnchor);
            while (seekAfter < current)
            {
                var witnessed = Interlocked.CompareExchange(ref sweepAnchor, seekAfter, current);
                if (witnessed == current)
                {
                    break;
                }

                current = witnessed;
            }
        }

        void MoveSweepPast(long seekAfter, long rowVersion)
        {
            var current = Interlocked.Read(ref sweepAnchor);
            // don't skip a sweep that a rollback or a head sweep has moved further back since this receive started
            while (current != NoSweep && current >= seekAfter && rowVersion > current)
            {
                var witnessed = Interlocked.CompareExchange(ref sweepAnchor, rowVersion, current);
                if (witnessed == current)
                {
                    break;
                }

                current = witnessed;
            }
        }


        const long NoSweep = long.MaxValue;

        long highestReceivedRowVersion;
        long sweepAnchor = NoSweep;
    }

    enum AnchorKind { HighestReceived, Sweep }
    readonly record struct ReceiveAnchor(long SeekAfter, AnchorKind Kind)
    {
        public static ReceiveAnchor HighestReceived(long seekAfter) => new(seekAfter, AnchorKind.HighestReceived);
        public static ReceiveAnchor Sweep(long seekAfter) => new(seekAfter, AnchorKind.Sweep);
    }
}
