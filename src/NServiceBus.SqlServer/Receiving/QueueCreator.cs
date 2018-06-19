#pragma warning disable 618
namespace NServiceBus.Transport.SQLServer
{
    using System.Data;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Transport;

    class QueueCreator : ICreateQueues
    {
        public QueueCreator(SqlConnectionFactory connectionFactory, QueueAddressTranslator addressTranslator, bool createMessageBodyColumn = false)
        {
            this.connectionFactory = connectionFactory;
            this.addressTranslator = addressTranslator;
            this.createMessageBodyColumn = createMessageBodyColumn;
        }

        public async Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            using (var transaction = connection.BeginTransaction())
            {
                foreach (var receivingAddress in queueBindings.ReceivingAddresses)
                {
                    await CreateQueue(addressTranslator.Parse(receivingAddress), connection, transaction, createMessageBodyColumn).ConfigureAwait(false);
                }

                foreach (var sendingAddress in queueBindings.SendingAddresses)
                {
                    await CreateQueue(addressTranslator.Parse(sendingAddress), connection, transaction, createMessageBodyColumn).ConfigureAwait(false);
                }
                transaction.Commit();
            }
        }

        static async Task CreateQueue(CanonicalQueueAddress canonicalQueueAddress, SqlConnection connection, SqlTransaction transaction, bool createMessageBodyColumn)
        {
            var sql = string.Format(SqlConstants.CreateQueueText, canonicalQueueAddress.QualifiedTableName, canonicalQueueAddress.QuotedCatalogName);
            using (var command = new SqlCommand(sql, connection, transaction)
            {
                CommandType = CommandType.Text
            })
            {
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            if (createMessageBodyColumn)
            {
                var bodyStringSql = string.Format(SqlConstants.AddMessageBodyStringColumn, canonicalQueueAddress.QualifiedTableName, canonicalQueueAddress.QuotedCatalogName);
                using (var command = new SqlCommand(bodyStringSql, connection, transaction)
                {
                    CommandType = CommandType.Text
                })
                {
                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        SqlConnectionFactory connectionFactory;
        QueueAddressTranslator addressTranslator;
        bool createMessageBodyColumn;
    }
}
#pragma warning restore 618