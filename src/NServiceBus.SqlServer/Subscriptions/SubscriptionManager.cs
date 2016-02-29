namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;

    class SubscriptionManager : IManageSubscriptions
    {
        string localEndpoint;
        string publicReceiveAddress;
        string subscriptionsSchema;
        string subscriptionsTable;
        SqlConnectionFactory connectionFactory;

        public SubscriptionManager(string localEndpoint, string publicReceiveAddress, string subscriptionsSchema, string subscriptionsTable, SqlConnectionFactory connectionFactory)
        {
            this.localEndpoint = localEndpoint;
            this.publicReceiveAddress = publicReceiveAddress;
            this.subscriptionsSchema = subscriptionsSchema;
            this.subscriptionsTable = subscriptionsTable;
            this.connectionFactory = connectionFactory;
        }

        public async Task Subscribe(Type eventType, ContextBag context)
        {
            using (var conn = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                using (var tx = conn.BeginTransaction())
                {
                    using (var cmd = new SqlCommand($@"DECLARE @dummy int; MERGE [{subscriptionsSchema}].[{subscriptionsTable}] WITH (HOLDLOCK) AS target
USING(SELECT @Endpoint AS Endpoint, @TransportAddress AS TransportAddress, @TypeName AS TypeName) AS source
      ON target.Endpoint = source.Endpoint AND target.TransportAddress = source.TransportAddress AND target.TypeName = source.TypeName
WHEN MATCHED THEN
    UPDATE SET @dummy = 0
WHEN NOT MATCHED THEN
    INSERT
      (
            Endpoint,
            TransportAddress,
            TypeName
      )
    VALUES
      (
            @Endpoint,
            @TransportAddress,
            @TypeName
      ); ", conn, tx))
                    {
                        cmd.Parameters.Add("@Endpoint", SqlDbType.NVarChar).Value = localEndpoint;
                        cmd.Parameters.Add("@TransportAddress", SqlDbType.NVarChar).Value = publicReceiveAddress;
                        cmd.Parameters.Add("@TypeName", SqlDbType.NVarChar).Value = eventType.FullName;
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                    tx.Commit();
                }
            }
        }

        public async Task Unsubscribe(Type eventType, ContextBag context)
        {
            using (var conn = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                using (var tx = conn.BeginTransaction())
                {
                    using (var cmd = new SqlCommand($@"DELETE FROM [{subscriptionsSchema}].[{subscriptionsTable}] WHERE Endpoint = @Endpoint AND TransportAddress = @TransportAddress AND TypeName = @TypeName", conn, tx))
                    {
                        cmd.Parameters.Add("@Endpoint", SqlDbType.NVarChar).Value = localEndpoint;
                        cmd.Parameters.Add("@TransportAddress", SqlDbType.NVarChar).Value = publicReceiveAddress;
                        cmd.Parameters.Add("@TypeName", SqlDbType.NVarChar).Value = eventType.FullName;
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                    tx.Commit();
                }
            }
        }
    }
}