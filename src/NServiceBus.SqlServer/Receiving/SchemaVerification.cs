namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Logging;

    class SchemaInspector
    {
        public SchemaInspector(Func<TableBasedQueue, Task<SqlConnection>> openConnection)
        {
            this.openConnection = openConnection;
        }

        public async Task PerformInspection(TableBasedQueue queue)
        {
            await VerifyExpiredIndex(queue).ConfigureAwait(false);
            await VerifyHeadersColumnType(queue).ConfigureAwait(false);
        }

        async Task VerifyExpiredIndex(TableBasedQueue queue)
        {
            try
            {
                using (var connection = await openConnection(queue).ConfigureAwait(false))
                {
                    var indexExists = await queue.CheckExpiresIndexPresence(connection).ConfigureAwait(false);
                    if (!indexExists)
                    {
                        Logger.Warn($@"Table {queue.Name} does not contain index 'Index_Expires'.{Environment.NewLine}Adding this index will speed up the process of purging expired messages from the queue. Please consult the documentation for further information.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WarnFormat("Checking indexes on table {0} failed. Exception: {1}", queue, ex);
            }
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
        static ILog Logger = LogManager.GetLogger<ExpiredMessagesPurger>();
    }
}