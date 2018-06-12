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
            var messageBodyComputedColumn = createMessageBodyColumn ? SqlConstants.MessageBodyStringColumn : string.Empty;
            var sql = string.Format(SqlConstants.CreateQueueText, canonicalQueueAddress.QualifiedTableName, canonicalQueueAddress.QuotedCatalogName, messageBodyComputedColumn);
            using (var command = new SqlCommand(sql, connection, transaction)
            {
                CommandType = CommandType.Text
            })
            {
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        SqlConnectionFactory connectionFactory;
        QueueAddressTranslator addressTranslator;
        readonly bool createMessageBodyColumn;
    }
}
#pragma warning restore 618