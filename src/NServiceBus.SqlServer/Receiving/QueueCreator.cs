namespace NServiceBus.Transports.SQLServer
{
    using System.Data;
    using System.Data.SqlClient;
    using System.Threading.Tasks;

    class QueueCreator : ICreateQueues
    {
        readonly SqlConnectionFactory connectionFactory;
        readonly QueueAddressParser addressParser;
        readonly EndpointConnectionStringLookup endpointConnectionLookup;

        public QueueCreator(SqlConnectionFactory connectionFactory, QueueAddressParser addressParser)
        public QueueCreator(SqlConnectionFactory connectionFactory, QueueAddressProvider addressProvider, EndpointConnectionStringLookup endpointConnectionLookup)
        {
            this.connectionFactory = connectionFactory;
            this.addressParser = addressParser;
            this.endpointConnectionLookup = endpointConnectionLookup;
        }

        public async Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            foreach (var receivingAddress in queueBindings.ReceivingAddresses)
            {
                var connectionString = endpointConnectionLookup.ConnectionStringLookup(receivingAddress).GetAwaiter().GetResult();
                using (var connection = await connectionFactory.OpenNewConnection(connectionString))
                {
                    using (var transaction = connection.BeginTransaction())
                    {
                        await CreateQueue(addressParser.Parse(receivingAddress), connection, transaction).ConfigureAwait(false);
                        transaction.Commit();
                    }
                }
            }
            foreach (var sendingAddress in queueBindings.SendingAddresses)
            {
                var connectionString = endpointConnectionLookup.ConnectionStringLookup(sendingAddress).GetAwaiter().GetResult();
                using (var connection = await connectionFactory.OpenNewConnection(connectionString))
                {
                    using (var transaction = connection.BeginTransaction())
                    {
                        await CreateQueue(addressProvider.Parse(sendingAddress), connection, transaction);
                        transaction.Commit();
                    }
                }
            }
        }

        async Task CreateQueue(QueueAddress address, SqlConnection connection, SqlTransaction transaction)
        {
            var sql = string.Format(Sql.CreateQueueText, address.SchemaName, address.TableName);

            using (var command = new SqlCommand(sql, connection, transaction)
            {
                CommandType = CommandType.Text
            })
            {
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }
    }
}