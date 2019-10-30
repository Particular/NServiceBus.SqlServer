#pragma warning disable 618
namespace NServiceBus.Transport.SQLServer
{
    using System.Data;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Transport;

    class QueueCreator : ICreateQueues
    {
        public QueueCreator(SqlConnectionFactory connectionFactory, QueueAddressTranslator addressTranslator, 
            LogicalAddress delayedQueueAddress, QualifiedSubscriptionTableName subscriptionTable,
            bool createMessageBodyColumn = false)
        {
            this.connectionFactory = connectionFactory;
            this.addressTranslator = addressTranslator;
            this.delayedQueueAddress = delayedQueueAddress;
            this.subscriptionTable = subscriptionTable;
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

            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            using (var transaction = connection.BeginTransaction())
            {
                await CreateDelayedMessageQueue(connection, transaction, createMessageBodyColumn).ConfigureAwait(false);
                await CreateSubscriptionsTable(connection, transaction).ConfigureAwait(false);
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

        async Task CreateDelayedMessageQueue(SqlConnection connection, SqlTransaction transaction, bool createMessageBodyComputedColumn)
        {
            var delayedQueue = GetDelayedQueueTableName();
#pragma warning disable 618
            var sql = string.Format(SqlConstants.CreateDelayedMessageStoreText, delayedQueue.QualifiedTableName, delayedQueue.QuotedCatalogName);
#pragma warning restore 618
            using (var command = new SqlCommand(sql, connection, transaction)
            {
                CommandType = CommandType.Text
            })
            {
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            if (createMessageBodyComputedColumn)
            {
#pragma warning disable 618
                var bodyStringSql = string.Format(SqlConstants.AddMessageBodyStringColumn, delayedQueue.QualifiedTableName, delayedQueue.QuotedCatalogName);
#pragma warning restore 618
                using (var command = new SqlCommand(bodyStringSql, connection, transaction)
                {
                    CommandType = CommandType.Text
                })
                {
                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }

            }
        }

        async Task CreateSubscriptionsTable(SqlConnection connection, SqlTransaction transaction)
        {
#pragma warning disable 618
            var sql = string.Format(SqlConstants.CreateSubscriptionTableText, subscriptionTable.QuotedQualifiedName, subscriptionTable.QuotedCatalog);
#pragma warning restore 618
            using (var command = new SqlCommand(sql, connection, transaction)
            {
                CommandType = CommandType.Text
            })
            {
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        CanonicalQueueAddress GetDelayedQueueTableName()
        {
            return addressTranslator.GetCanonicalForm(addressTranslator.Generate(delayedQueueAddress));
        }

        SqlConnectionFactory connectionFactory;
        QueueAddressTranslator addressTranslator;
        LogicalAddress delayedQueueAddress;
        QualifiedSubscriptionTableName subscriptionTable;
        bool createMessageBodyColumn;
    }
}
#pragma warning restore 618