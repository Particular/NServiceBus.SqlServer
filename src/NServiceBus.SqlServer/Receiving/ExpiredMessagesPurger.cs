namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using NServiceBus.Settings;

    interface IPurgeExpiredMessages
    {
        Task Purge(TableBasedQueue queue, CancellationToken cancellationToken);
    }

    class ExpiredMessagesPurger : IPurgeExpiredMessages
    {
        public ExpiredMessagesPurger(SettingsHolder settings, Func<TableBasedQueue, Task<SqlConnection>> openConnection)
        {
            this.openConnection = openConnection;

            if (!settings.TryGet(SettingsKeys.PurgeTaskDelayKey, out purgeTaskDelay))
            {
                purgeTaskDelay = DefaultPurgeTaskDelay;
            }

            if (!settings.TryGet(SettingsKeys.PurgeBatchSizeKey, out purgeBatchSize))
            {
                purgeBatchSize = DefaultPurgeBatchSize;
            }
        }

        public async Task Purge(TableBasedQueue queue, CancellationToken cancellationToken)
        {
            await LogWarningWhenIndexIsMissing(queue).ConfigureAwait(false);

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
                        continuePurging = (purgedRowsCount == purgeBatchSize);
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

            Logger.DebugFormat("Scheduling next expired message purge task for table {0} in {1}", queue, purgeTaskDelay);
            await Task.Delay(purgeTaskDelay, cancellationToken).ConfigureAwait(false);
        }

        async Task LogWarningWhenIndexIsMissing(TableBasedQueue queue)
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

        Func<TableBasedQueue, Task<SqlConnection>> openConnection;

        TimeSpan purgeTaskDelay;
        int purgeBatchSize;

        static readonly TimeSpan DefaultPurgeTaskDelay = TimeSpan.FromMinutes(5);
        const int DefaultPurgeBatchSize = 10000;

        static ILog Logger = LogManager.GetLogger<ExpiredMessagesPurger>();
    }
}