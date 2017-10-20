namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;

    class ExpiredMessagesPurger
    {
        public ExpiredMessagesPurger(Func<TableBasedQueue, Task<SqlConnection>> openConnection, int? purgeBatchSize, bool enable)
        {
            this.openConnection = openConnection;
            this.enable = enable;
            this.purgeBatchSize = purgeBatchSize ?? DefaultPurgeBatchSize;
        }

        public async Task Purge(TableBasedQueue queue, CancellationToken cancellationToken)
        {
            if (!enable)
            {
                Logger.DebugFormat("Purging expired messages on startup is not enabled for {0}.", queue);
                return;
            }

            Logger.DebugFormat("Starting a new expired message purge task for table {0}.", queue);
            var totalPurgedRowsCount = 0;

            try
            {
                using (var connection = await openConnection(queue).ConfigureAwait(false))
                {
                    var continuePurging = true;

                    while (continuePurging && !cancellationToken.IsCancellationRequested)
                    {
                        var purgedRowsCount = await queue.PurgeBatchOfExpiredMessages(connection, purgeBatchSize).ConfigureAwait(false);

                        totalPurgedRowsCount += purgedRowsCount;
                        continuePurging = purgedRowsCount == purgeBatchSize;
                    }
                }

                Logger.DebugFormat("{0} expired messages were successfully purged from table {1}", totalPurgedRowsCount, queue);
            }
            catch
            {
                Logger.WarnFormat("Purging expired messages from table {0} failed after purging {1} messages.", queue, totalPurgedRowsCount);
                throw;
            }
        }

        int purgeBatchSize;
        Func<TableBasedQueue, Task<SqlConnection>> openConnection;
        readonly bool enable;
        const int DefaultPurgeBatchSize = 10000;
        static ILog Logger = LogManager.GetLogger<ExpiredMessagesPurger>();
    }
}