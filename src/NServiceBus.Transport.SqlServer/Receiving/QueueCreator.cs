#pragma warning disable 618
namespace NServiceBus.Transport.SqlServer
{
    using System.Data;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Threading.Tasks;
    using Transport;

    class QueueCreator : ICreateQueues
    {
        public QueueCreator(SqlConnectionFactory connectionFactory, QueueAddressTranslator addressTranslator,
            CanonicalQueueAddress delayedQueueAddress, bool createMessageBodyColumn = false)
        {
            this.connectionFactory = connectionFactory;
            this.addressTranslator = addressTranslator;
            this.delayedQueueAddress = delayedQueueAddress;
            this.createMessageBodyColumn = createMessageBodyColumn;
        }

        public async Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            using (var transaction = connection.BeginTransaction())
            {
                foreach (var receivingAddress in queueBindings.ReceivingAddresses)
                {
                    await CreateQueue(SqlConstants.CreateQueueText, addressTranslator.Parse(receivingAddress), connection, transaction, createMessageBodyColumn).ConfigureAwait(false);
                }

                foreach (var sendingAddress in queueBindings.SendingAddresses)
                {
                    await CreateQueue(SqlConstants.CreateQueueText, addressTranslator.Parse(sendingAddress), connection, transaction, createMessageBodyColumn).ConfigureAwait(false);
                }

                if (delayedQueueAddress != null)
                {
                    await CreateQueue(SqlConstants.CreateDelayedMessageStoreText, delayedQueueAddress, connection, transaction, createMessageBodyColumn).ConfigureAwait(false);
                }
                transaction.Commit();
            }
        }

        static async Task CreateQueue(string creationScript, CanonicalQueueAddress canonicalQueueAddress, SqlConnection connection, SqlTransaction transaction, bool createMessageBodyColumn)
        {
            var sql = string.Format(creationScript, canonicalQueueAddress.QualifiedTableName, canonicalQueueAddress.QuotedCatalogName);
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
        CanonicalQueueAddress delayedQueueAddress;
        bool createMessageBodyColumn;
    }
}
#pragma warning restore 618