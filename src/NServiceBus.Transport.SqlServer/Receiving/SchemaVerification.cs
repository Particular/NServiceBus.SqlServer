namespace NServiceBus.Transport.SqlServer
{
    using System;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Threading.Tasks;
    using Logging;

    class SchemaInspector
    {
        public SchemaInspector(Func<TableBasedQueue, Task<SqlConnection>> openConnection, bool validateExpiredIndex)
        {
            this.openConnection = openConnection;
            this.validateExpiredIndex = validateExpiredIndex;
        }

        public async Task PerformInspection(TableBasedQueue queue)
        {
            if (validateExpiredIndex)
            {
                await VerifyExpiredIndex(queue).ConfigureAwait(false);
            }

            await VerifyNonClusteredRowVersionIndex(queue).ConfigureAwait(false);
            await VerifyHeadersColumnType(queue).ConfigureAwait(false);
        }

        async Task VerifyIndex(TableBasedQueue queue, Func<TableBasedQueue, SqlConnection, Task<bool>> check, string noIndexMessage)
        {
            try
            {
                using (var connection = await openConnection(queue).ConfigureAwait(false))
                {
                    var indexExists = await check(queue, connection).ConfigureAwait(false);
                    
                    if (!indexExists)
                    {
                        Logger.Warn(noIndexMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WarnFormat("Checking indexes on table {0} failed. Exception: {1}", queue, ex);
            }
        }
        Task VerifyNonClusteredRowVersionIndex(TableBasedQueue queue)
        {
            return VerifyIndex(
                queue,
                (q, c) => q.CheckNonClusteredRowVersionIndexPresence(c),
                $"Table {queue.Name} does not contain non-clustered index for column 'RowVersion'.{Environment.NewLine}Migrating to this non-clustered index improves performance for send and receive operations.");
        }

        Task VerifyExpiredIndex(TableBasedQueue queue)
        {
            return VerifyIndex(
                queue,
                (q, c) => q.CheckExpiresIndexPresence(c),
                $"Table {queue.Name} does not contain index for column 'Expires'.{Environment.NewLine}Adding this index will speed up the process of purging expired messages from the queue. Please consult the documentation for further information."
            );
        }

        async Task VerifyHeadersColumnType(TableBasedQueue queue)
        {
            try
            {
                using (var connection = await openConnection(queue).ConfigureAwait(false))
                {
                    var columnType = await queue.CheckHeadersColumnType(connection).ConfigureAwait(false);
                    if (string.Equals(columnType, "varchar", StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Warn($"Table {queue.Name} stores headers in a non Unicode-compatible column (varchar).{Environment.NewLine}This may lead to data loss when sending non-ASCII characters in headers. SQL Server transport 3.1 and newer can take advantage of the nvarchar column type for headers. Please change the column type in the database.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WarnFormat("Checking indexes on table {0} failed. Exception: {1}", queue, ex);
            }
        }

        Func<TableBasedQueue, Task<SqlConnection>> openConnection;
        readonly bool validateExpiredIndex;
        static ILog Logger = LogManager.GetLogger<ExpiredMessagesPurger>();
    }
}