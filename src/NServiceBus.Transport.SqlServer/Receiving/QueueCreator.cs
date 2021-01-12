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

    class QueueCreator
    {
        public QueueCreator(SqlConnectionFactory connectionFactory, QueueAddressTranslator addressTranslator, bool createMessageBodyColumn = false)
        {
            this.connectionFactory = connectionFactory;
            this.addressTranslator = addressTranslator;
            this.createMessageBodyColumn = createMessageBodyColumn;
        }

        public async Task CreateQueueIfNecessary(string[] queueAddresses, CanonicalQueueAddress delayedQueueAddress)
        {
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            using (var transaction = connection.BeginTransaction())
            {
                foreach (var address in queueAddresses)
                {
                    await CreateQueue(SqlConstants.CreateQueueText, addressTranslator.Parse(address), connection, transaction, createMessageBodyColumn).ConfigureAwait(false);
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
        bool createMessageBodyColumn;
    }
}
#pragma warning restore 618