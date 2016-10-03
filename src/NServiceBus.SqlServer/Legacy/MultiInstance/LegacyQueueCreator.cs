namespace NServiceBus.Transport.SQLServer
{
    using System.Data;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Transport;

    class LegacyQueueCreator : ICreateQueues
    {
        public LegacyQueueCreator(LegacySqlConnectionFactory connectionFactory, QueueAddressParser addressParser)
        {
            this.connectionFactory = connectionFactory;
            this.addressParser = addressParser;
        }

        public async Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            foreach (var receivingAddress in queueBindings.ReceivingAddresses)
            {
                using (var connection = await connectionFactory.OpenNewConnection(receivingAddress).ConfigureAwait(false))
                using (var transaction = connection.BeginTransaction())
                {
                    await CreateQueue(addressParser.Parse(receivingAddress), connection, transaction).ConfigureAwait(false);
                    transaction.Commit();
                }
            }

            foreach (var sendingAddress in queueBindings.SendingAddresses)
            {
                using (var connection = await connectionFactory.OpenNewConnection(sendingAddress).ConfigureAwait(false))
                using (var transaction = connection.BeginTransaction())
                {
                    await CreateQueue(addressParser.Parse(sendingAddress), connection, transaction).ConfigureAwait(false);
                    transaction.Commit();
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

        LegacySqlConnectionFactory connectionFactory;
        QueueAddressParser addressParser;
    }
}