namespace NServiceBus.Transports.SQLServer
{
    using System.Data;
    using System.Data.SqlClient;
    using System.Threading.Tasks;

    class QueueCreator : ICreateQueues
    {
        readonly SqlConnectionFactory connectionFactory;
        readonly QueueAddressParser addressParser;

        public QueueCreator(SqlConnectionFactory connectionFactory, QueueAddressParser addressParser)
        {
            this.connectionFactory = connectionFactory;
            this.addressParser = addressParser;
        }

        public async Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                using (var transaction = connection.BeginTransaction())
                {
                    foreach (var receivingAddress in queueBindings.ReceivingAddresses)
                    {
                        await CreateQueue(addressParser.Parse(receivingAddress), connection, transaction).ConfigureAwait(false);
                    }
                    foreach (var receivingAddress in queueBindings.SendingAddresses)
                    {
                        await CreateQueue(addressParser.Parse(receivingAddress), connection, transaction).ConfigureAwait(false);
                    }

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
    }
}