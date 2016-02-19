namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading;
    using NServiceBus.Logging;

    class ExpiredMessagesPurger : IExecutor
    {
        static readonly TimeSpan purgeTaskDelay = TimeSpan.FromMinutes(5);

        static readonly ILog Logger = LogManager.GetLogger(typeof(ExpiredMessagesPurger));

        readonly TableBasedQueue queue;
        readonly Func<SqlConnection> openConnection;

        Timer purgeTaskTimer;
        CancellationToken token;

        object lockObject = new object();

        public ExpiredMessagesPurger(TableBasedQueue queue, Func<SqlConnection> openConnection)
        {
            this.queue = queue;
            this.openConnection = openConnection;
        }

        public void Start(int maximumConcurrency, CancellationToken token)
        {
            LogWarningWhenIndexIsMissing();

            this.token = token;
            purgeTaskTimer = new Timer(PurgeExpiredMessagesCallback, null, TimeSpan.Zero, purgeTaskDelay);
        }

        public void Stop()
        {
            using (var waitHandle = new ManualResetEvent(false))
            {
                purgeTaskTimer.Dispose(waitHandle);
                waitHandle.WaitOne();
            }
        }

        void LogWarningWhenIndexIsMissing()
        {
            try
            {
                using (var connection = openConnection())
                {
                    queue.LogWarningWhenIndexIsMissing(connection);
                }
            }
            catch (Exception ex)
            {
                Logger.WarnFormat("Checking indexes on table {0} failed. Exception: {1}", queue, ex);
            }
        }

        void PurgeExpiredMessagesCallback(object state)
        {
            Logger.DebugFormat("Starting a new expired message purge task for table {0}.", queue);

            if (token.IsCancellationRequested)
            {
                return;
            }

            var lockTaken = false;
            try
            {
                Monitor.TryEnter(lockObject, ref lockTaken);
                if (!lockTaken)
                {
                    Logger.DebugFormat("An expired message purge task for table {0} is already running. Nothing to do.", queue);
                    return;
                }

                PurgeExpiredMessages();
            }
            finally
            {
                if (lockTaken)
                {
                    Monitor.Exit(lockObject);
                }
            }
        }

        void PurgeExpiredMessages()
        {
            int totalPurgedRowsCount = 0;

            try
            {
                using (var connection = openConnection())
                {
                    var continuePurging = true;

                    while (continuePurging && !token.IsCancellationRequested)
                    {
                        var purgedRowsCount = queue.PurgeBatchOfExpiredMessages(connection);

                        totalPurgedRowsCount += purgedRowsCount;
                        continuePurging = (purgedRowsCount == TableBasedQueue.PurgeBatchSize);
                    }
                }

                if (totalPurgedRowsCount > 0)
                {
                    Logger.InfoFormat("{0} expired messages were successfully purged from table {1}", totalPurgedRowsCount, queue);
                }
            }
            catch (Exception ex)
            {
                Logger.WarnFormat("Purging expired messages from table {0} failed after purging {1} messages. Exception: {2}", queue, totalPurgedRowsCount, ex);
            }
        }
    }
}