namespace NServiceBus.Transport.SqlServer
{
    using System;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;

    class ExpiredMessagesPurger : IExpiredMessagesPurger
    {
        public ExpiredMessagesPurger(Func<TableBasedQueue, CancellationToken, Task<SqlConnection>> openConnection, int? purgeBatchSize)
        {
            this.openConnection = openConnection;
            this.purgeBatchSize = purgeBatchSize ?? DefaultPurgeBatchSize;
        }

        public async Task Purge(TableBasedQueue queue, CancellationToken cancellationToken = default)
        {
            Logger.DebugFormat("Starting a new expired message purge task for table {0}.", queue);
            var totalPurgedRowsCount = 0;

            try
            {
                using (var connection = await openConnection(queue, cancellationToken).ConfigureAwait(false))
                {
                    var continuePurging = true;

                    while (continuePurging)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var purgedRowsCount = await queue.PurgeBatchOfExpiredMessages(connection, purgeBatchSize, cancellationToken).ConfigureAwait(false);

                        totalPurgedRowsCount += purgedRowsCount;
                        continuePurging = purgedRowsCount == purgeBatchSize;
                    }
                }

                Logger.DebugFormat("{0} expired messages were successfully purged from table {1}", totalPurgedRowsCount, queue);
            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                Logger.WarnFormat("Purging expired messages from table {0} failed after purging {1} messages.", queue, totalPurgedRowsCount);
                throw;
            }
        }

        int purgeBatchSize;
        Func<TableBasedQueue, CancellationToken, Task<SqlConnection>> openConnection;
        const int DefaultPurgeBatchSize = 10000;
        static ILog Logger = LogManager.GetLogger<ExpiredMessagesPurger>();
    }
}