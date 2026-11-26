namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using NServiceBus.Transport.Sql.Shared;

    class SchemaInspector(Func<TableBasedQueue, CancellationToken, Task<DbConnection>> openConnection, bool validateExpiredIndex, bool reportHeapLayout)
    {
        public async Task PerformInspection(SqlTableBasedQueue queue, CancellationToken cancellationToken = default)
        {
            if (validateExpiredIndex)
            {
                await VerifyExpiredIndex(queue, cancellationToken).ConfigureAwait(false);
            }

            await VerifyNonClusteredRowVersionIndex(queue, cancellationToken).ConfigureAwait(false);
            await VerifyHeadersColumnType(queue, cancellationToken).ConfigureAwait(false);

            if (reportHeapLayout)
            {
                await ReportHeapLayout(queue, cancellationToken).ConfigureAwait(false);
            }
        }

        async Task VerifyIndex(SqlTableBasedQueue queue, Func<SqlTableBasedQueue, DbConnection, CancellationToken, Task<bool>> check, string noIndexMessage, CancellationToken cancellationToken)
        {
            try
            {
                var connection = await openConnection(queue, cancellationToken).ConfigureAwait(false);
                await using var connectionDisposer = connection.ConfigureAwait(false);
                var indexExists = await check(queue, connection, cancellationToken).ConfigureAwait(false);

                if (!indexExists)
                {
                    Logger.Warn(noIndexMessage);
                }
            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                Logger.WarnFormat("Checking indexes on table {0} failed. Exception: {1}", queue, ex);
            }
        }
        Task VerifyNonClusteredRowVersionIndex(SqlTableBasedQueue queue, CancellationToken cancellationToken) =>
            VerifyIndex(
                queue,
                (q, c, token) => q.CheckNonClusteredRowVersionIndexPresence(c, token),
                $"Table {queue.Name} does not contain non-clustered index for column 'RowVersion'.{Environment.NewLine}Migrating to this non-clustered index improves performance for send and receive operations.",
                cancellationToken);

        Task VerifyExpiredIndex(SqlTableBasedQueue queue, CancellationToken cancellationToken) =>
            VerifyIndex(
                queue,
                (q, c, token) => q.CheckExpiresIndexPresence(c, token),
                $"Table {queue.Name} does not contain index for column 'Expires'.{Environment.NewLine}Adding this index will speed up the process of purging expired messages from the queue. Please consult the documentation for further information.",
                cancellationToken
            );

        async Task VerifyHeadersColumnType(SqlTableBasedQueue queue, CancellationToken cancellationToken)
        {
            try
            {
                var connection = await openConnection(queue, cancellationToken).ConfigureAwait(false);
                await using var connectionDisposer = connection.ConfigureAwait(false);
                var columnType = await queue.CheckHeadersColumnType(connection, cancellationToken).ConfigureAwait(false);
                if (string.Equals(columnType, "varchar", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Warn($"Table {queue.Name} stores headers in a non Unicode-compatible column (varchar).{Environment.NewLine}This may lead to data loss when sending non-ASCII characters in headers. SQL Server transport 3.1 and newer can take advantage of the nvarchar column type for headers. Please change the column type in the database.");
                }
            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                Logger.WarnFormat("Checking indexes on table {0} failed. Exception: {1}", queue, ex);
            }
        }

        async Task ReportHeapLayout(SqlTableBasedQueue queue, CancellationToken cancellationToken)
        {
            try
            {
                var connection = await openConnection(queue, cancellationToken).ConfigureAwait(false);
                await using var connectionDisposer = connection.ConfigureAwait(false);
                if (await queue.CheckIfTableIsHeap(connection, cancellationToken).ConfigureAwait(false))
                {
                    Logger.Info($"Table {queue.Name} uses the heap layout and keeps it. {nameof(SqlServerTransport.CreateBucketedQueueTables)} applies only to queue tables the transport creates.");
                }
            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                Logger.WarnFormat("Checking the layout of table {0} failed. Exception: {1}", queue, ex);
            }
        }

        Func<SqlTableBasedQueue, CancellationToken, Task<DbConnection>> openConnection = openConnection;
        static ILog Logger = LogManager.GetLogger<SchemaInspector>();
    }
}