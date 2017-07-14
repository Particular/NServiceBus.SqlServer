namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using NServiceBus.Logging;
    using NServiceBus.Transports.SQLServer;

    class SchemaInspector
    {
        static readonly ILog Logger = LogManager.GetLogger<SchemaInspector>();

        Func<SqlConnection> openConnection;

        public SchemaInspector(Func<SqlConnection> openConnection)
        {
            this.openConnection = openConnection;
        }

        public void PerformInspection(TableBasedQueue queue)
        {
            VerifyExpiredIndex(queue);
            VerifyHeadersColumnType(queue);
        }

        void VerifyExpiredIndex(TableBasedQueue queue)
        {
            try
            {
                using (var connection = openConnection())
                {
                    var indexExists = queue.CheckExpiresIndexPresence(connection);
                    if (!indexExists)
                    {
                        Logger.Warn($@"Table {queue} does not contain index 'Index_Expires'.{Environment.NewLine}Adding this index will speed up the process of purging expired messages from the queue. Please consult the documentation for further information.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WarnFormat("Checking indexes on table {0} failed. Exception: {1}", queue, ex);
            }
        }

        void VerifyHeadersColumnType(TableBasedQueue queue)
        {
            try
            {
                using (var connection = openConnection())
                {
                    var columnType = queue.CheckHeadersColumnType(connection);
                    if (string.Equals(columnType, "varchar", StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Warn($"Table {queue} stores headers in a non Unicode-compatible column (varchar).{Environment.NewLine}This may lead to data loss when sending non-ASCII characters in headers. SQL Server transport 3.1 and newer can take advantage of the nvarchar column type for headers. Please change the column type in the database.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WarnFormat("Checking column type on table {0} failed. Exception: {1}", queue, ex);
            }
        }
    }
}