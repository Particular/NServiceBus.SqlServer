#pragma warning disable 618
namespace NServiceBus.Transport.SQLServer
{
    using System.Data;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Transport;

    class LegacyQueueCreator : ICreateQueues
    {
        public LegacyQueueCreator(LegacySqlConnectionFactory connectionFactory, LegacyQueueAddressTranslator addressTranslator)
        {
            this.connectionFactory = connectionFactory;
            this.addressTranslator = addressTranslator;
        }

        public async Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            foreach (var receivingAddress in queueBindings.ReceivingAddresses)
            {
                using (var connection = await connectionFactory.OpenNewConnection(receivingAddress).ConfigureAwait(false))
                using (var transaction = connection.BeginTransaction())
                {
                    await CreateQueue(addressTranslator.Parse(receivingAddress).QualifiedTableName, connection, transaction).ConfigureAwait(false);
                    transaction.Commit();
                }
            }

            foreach (var sendingAddress in queueBindings.SendingAddresses)
            {
                using (var connection = await connectionFactory.OpenNewConnection(sendingAddress).ConfigureAwait(false))
                using (var transaction = connection.BeginTransaction())
                {
                    await CreateQueue(addressTranslator.Parse(sendingAddress).QualifiedTableName, connection, transaction).ConfigureAwait(false);
                    transaction.Commit();
                }
            }
        }

        static async Task CreateQueue(string qualifiedTableName, SqlConnection connection, SqlTransaction transaction)
        {
            var sql = string.Format(LegacySql.CreateQueueText, qualifiedTableName);

            using (var command = new SqlCommand(sql, connection, transaction)
            {
                CommandType = CommandType.Text
            })
            {
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

        }

        LegacySqlConnectionFactory connectionFactory;
        LegacyQueueAddressTranslator addressTranslator;
    }
}