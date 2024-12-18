namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using NServiceBus.Transport.Sql.Shared;

    class ExpiredMessagesPurger : IExpiredMessagesPurger
    {
        public ExpiredMessagesPurger(Func<TableBasedQueue, CancellationToken, Task<DbConnection>> openConnection, int? purgeBatchSize, IExceptionClassifier exceptionClassifier)
        {
            this.openConnection = openConnection;
            this.exceptionClassifier = exceptionClassifier;
            this.purgeBatchSize = purgeBatchSize ?? DefaultPurgeBatchSize;
        }

        public async Task Purge(SqlTableBasedQueue queue, CancellationToken cancellationToken = default)
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
            catch (Exception ex) when (!exceptionClassifier.IsOperationCancelled(ex, cancellationToken))
            {
                Logger.WarnFormat("Purging expired messages from table {0} failed after purging {1} messages.", queue, totalPurgedRowsCount);
                throw;
            }
        }

        int purgeBatchSize;
        Func<TableBasedQueue, CancellationToken, Task<DbConnection>> openConnection;
        readonly IExceptionClassifier exceptionClassifier;
        const int DefaultPurgeBatchSize = 10000;
        static ILog Logger = LogManager.GetLogger<ExpiredMessagesPurger>();
    }
}