#pragma warning disable 618
namespace NServiceBus.Transport.SqlServer
{
    using System;
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
            CanonicalQueueAddress delayedQueueAddress, bool createMessageBodyColumn = false,
            bool useLeaseBasedReceive = true)
        {
            this.connectionFactory = connectionFactory;
            this.addressTranslator = addressTranslator;
            this.delayedQueueAddress = delayedQueueAddress;
            this.createMessageBodyColumn = createMessageBodyColumn;
            this.useLeaseBasedReceive = useLeaseBasedReceive;
        }

        public async Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            using (var transaction = connection.BeginTransaction())
            {
                var queueCreationText = useLeaseBasedReceive ? SqlConstants.LeaseBasedCreateQueueText : SqlConstants.CreateQueueText;

                foreach (var receivingAddress in queueBindings.ReceivingAddresses)
                {
                    await CreateQueue(queueCreationText, addressTranslator.Parse(receivingAddress), connection, transaction, createMessageBodyColumn).ConfigureAwait(false);
                }

                foreach (var sendingAddress in queueBindings.SendingAddresses)
                {
                    await CreateQueue(queueCreationText, addressTranslator.Parse(sendingAddress), connection, transaction, createMessageBodyColumn).ConfigureAwait(false);
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
            try
            {
                var sql = string.Format(creationScript, canonicalQueueAddress.QualifiedTableName, canonicalQueueAddress.QuotedCatalogName);
                using (var command = new SqlCommand(sql, connection, transaction)
                {
                    CommandType = CommandType.Text
                })
                {
                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
            catch (Exception e) when (e.Message.StartsWith("There is already an object named") || e.Message.StartsWith("The operation failed because an index or statistics with name"))
            {
                // ignored because of race when multiple endpoints start
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
        readonly bool useLeaseBasedReceive;
    }
}
#pragma warning restore 618