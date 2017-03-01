namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Transport;

    class QueueCreator : ICreateQueues
    {
        public QueueCreator(SqlConnectionFactory connectionFactory, QueueAddressParser addressParser)
        {
            this.connectionFactory = connectionFactory;
            this.addressParser = addressParser;
        }

        public async Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            using (var transaction = connection.BeginTransaction())
            {
                await Task.WhenAll(Create(queueBindings, connection, transaction)).ConfigureAwait(false);
                transaction.Commit();
            }
        }

        IEnumerable<Task> Create(QueueBindings queueBindings, SqlConnection connection, SqlTransaction transaction)
        {
            foreach (var receivingAddress in queueBindings.ReceivingAddresses)
            {
                yield return CreateQueue(addressParser.Parse(receivingAddress), connection, transaction);
            }

            foreach (var receivingAddress in queueBindings.SendingAddresses)
            {
                yield return CreateQueue(addressParser.Parse(receivingAddress), connection, transaction);
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

        SqlConnectionFactory connectionFactory;
        QueueAddressParser addressParser;
    }
}