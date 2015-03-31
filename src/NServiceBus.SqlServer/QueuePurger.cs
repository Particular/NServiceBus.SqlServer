namespace NServiceBus.Transports.SQLServer
{
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using NServiceBus.Features;
    using NServiceBus.Logging;

    class QueuePurger : IQueuePurger
    {
        readonly LocalConnectionParams localConnectionParams;

        public QueuePurger(SecondaryReceiveConfiguration secondaryReceiveConfiguration, LocalConnectionParams localConnectionParams)
        {
            this.secondaryReceiveConfiguration = secondaryReceiveConfiguration;
            this.localConnectionParams = localConnectionParams;
        }

        public void Purge(string address)
        {
            Purge(AllTables(address));
        }

        void Purge(IEnumerable<string> tableNames)
        {
            using (var connection = new SqlConnection(localConnectionParams.ConnectionString))
            {
                connection.Open();

                foreach (var tableName in tableNames)
                {
                    using (var command = new SqlCommand(string.Format(SqlPurge, localConnectionParams.Schema, tableName), connection)
                    {
                        CommandType = CommandType.Text
                    })
                    {
                        var numberOfPurgedRows = command.ExecuteNonQuery();

                        Logger.InfoFormat("{0} messages was purged from table {1}", numberOfPurgedRows, tableName);
                    }
                }
            }
        }

        IEnumerable<string> AllTables(string address)
        {
            var settings = secondaryReceiveConfiguration.GetSettings(address);
            yield return address.GetTableName();
            if (settings.IsEnabled)
            {
                yield return settings.ReceiveQueue;
            }
        }

        const string SqlPurge = @"DELETE FROM [{0}].[{1}]";

        static readonly ILog Logger = LogManager.GetLogger(typeof(QueuePurger));

        readonly SecondaryReceiveConfiguration secondaryReceiveConfiguration;
    }
}