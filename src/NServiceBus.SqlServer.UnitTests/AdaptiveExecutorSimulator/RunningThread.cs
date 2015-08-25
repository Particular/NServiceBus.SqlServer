namespace NServiceBus.SqlServer.UnitTests
{
    using System;
    using System.Collections.Generic;

    class RunningThread
    {
        long dueAt;
        long blockedAt;
        readonly int threadNumber;
        readonly IEnumerator<long> processingMethod;
        readonly Action<RunningThread> diedCallback;
        bool replaced;
        bool dead;
        bool longRunning;

        public RunningThread(int threadNumber, IEnumerator<long> processingMethod, Action<RunningThread> diedCallback, long dueAt)
        {
            this.threadNumber = threadNumber;
            this.processingMethod = processingMethod;
            this.diedCallback = diedCallback;
            this.dueAt = dueAt;
        }

        public long BlockedAt
        {
            get { return blockedAt; }
        }

        public long DueAt
        {
            get { return dueAt; }
        }

        public int ThreadNumber
        {
            get { return threadNumber; }
        }

        public bool Dead
        {
            get { return dead; }
        }

        public bool Replaced
        {
            get { return replaced; }
        }

        public bool LongRunning
        {
            get { return longRunning; }
        }

        public void MarkAsReplaced()
        {
            replaced = true;
        }

        public void Tick(long currentTime)
        {
            if (currentTime < dueAt)
            {
                return;
            }
            var stillAlive = processingMethod.MoveNext();
            blockedAt = currentTime;
            replaced = false;
            if (stillAlive)
            {
                var processingTime = processingMethod.Current;
                dueAt = currentTime + processingTime;
                longRunning = processingTime > 5000;
            }
            else
            {
                dead = true;
                diedCallback(this);
            }
        }
    }
}