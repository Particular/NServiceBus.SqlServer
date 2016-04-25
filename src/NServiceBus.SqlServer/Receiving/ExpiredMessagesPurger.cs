namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;

    class ExpiredMessagesPurger
    {
        public ExpiredMessagesPurger(Func<ITableBasedQueue, Task<SqlConnection>> openConnection, TimeSpan? purgeTaskDelay, int? purgeBatchSize)
        {
            this.openConnection = openConnection;

            PurgeTaskDelay = purgeTaskDelay ?? DefaultPurgeTaskDelay;
            PurgeBatchSize = purgeBatchSize ?? DefaultPurgeBatchSize;
        }

        public TimeSpan PurgeTaskDelay { get; }

        int PurgeBatchSize { get; }

        public async Task Purge(ITableBasedQueue queue, CancellationToken cancellationToken)
        {
            Logger.DebugFormat("Starting a new expired message purge task for table {0}.", queue);

            var totalPurgedRowsCount = 0;

            try
            {
                using (var connection = await openConnection(queue).ConfigureAwait(false))
                {
                    var continuePurging = true;

                    while (continuePurging && !cancellationToken.IsCancellationRequested)
                    {
                        var purgedRowsCount = await queue.PurgeBatchOfExpiredMessages(connection, PurgeBatchSize).ConfigureAwait(false);

                        totalPurgedRowsCount += purgedRowsCount;
                        continuePurging = (purgedRowsCount == PurgeBatchSize);
                    }
                }

                Logger.DebugFormat("{0} expired messages were successfully purged from table {1}", totalPurgedRowsCount, queue);
            }
            catch (Exception ex)
            {
                Logger.WarnFormat("Purging expired messages from table {0} failed after purging {1} messages. Exception: {2}", queue, totalPurgedRowsCount, ex);
            }
        }

        public async Task Initialize(ITableBasedQueue queue)
        {
            try
            {
                using (var connection = await openConnection(queue).ConfigureAwait(false))
                {
                    await queue.LogWarningWhenIndexIsMissing(connection).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Logger.WarnFormat("Checking indexes on table {0} failed. Exception: {1}", queue, ex);
            }
        }

        Func<ITableBasedQueue, Task<SqlConnection>> openConnection;
        const int DefaultPurgeBatchSize = 10000;

        static TimeSpan DefaultPurgeTaskDelay = TimeSpan.FromMinutes(5);

        static ILog Logger = LogManager.GetLogger<ExpiredMessagesPurger>();
    }
}