namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading;
    using NServiceBus.Logging;

    class ExpiredMessagesPurger : IExecutor
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(ExpiredMessagesPurger));

        readonly TableBasedQueue queue;
        readonly Func<SqlConnection> openConnection;
        readonly PurgeExpiredMessagesParams parameters;

        Timer purgeTaskTimer;
        CancellationToken token;

        public ExpiredMessagesPurger(TableBasedQueue queue, Func<SqlConnection> openConnection, PurgeExpiredMessagesParams parameters)
        {
            this.queue = queue;
            this.openConnection = openConnection;
            this.parameters = parameters;
        }

        public void Start(int maximumConcurrency, CancellationToken token)
        {
            this.token = token;
            purgeTaskTimer = new Timer(PurgeExpiredMessagesCallback, null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }

        public void Stop()
        {
            using (var waitHandle = new ManualResetEvent(false))
            {
                purgeTaskTimer.Dispose(waitHandle);
                waitHandle.WaitOne();
            }
        }

        void PurgeExpiredMessagesCallback(object state)
        {
            Logger.DebugFormat("Starting a new expired message purge task for table {0}.", queue);

            if (token.IsCancellationRequested)
            {
                return;
            }

            PurgeExpiredMessages();

            if (token.IsCancellationRequested)
            {
                return;
            }

            Logger.DebugFormat("Scheduling next expired message purge task for table {0} in {1}", queue, parameters.PurgeTaskDelay);
            purgeTaskTimer.Change(parameters.PurgeTaskDelay, Timeout.InfiniteTimeSpan);
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
                        var purgedRowsCount = queue.PurgeBatchOfExpiredMessages(connection, parameters.PurgeBatchSize);

                        totalPurgedRowsCount += purgedRowsCount;
                        continuePurging = (purgedRowsCount == parameters.PurgeBatchSize);
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