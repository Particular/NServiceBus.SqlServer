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
            {
                foreach (var address in queueAddresses)
                {
                    await CreateQueue(SqlConstants.CreateQueueText, addressTranslator.Parse(address), connection, createMessageBodyColumn).ConfigureAwait(false);
                }

                if (delayedQueueAddress != null)
                {
                    await CreateQueue(SqlConstants.CreateDelayedMessageStoreText, delayedQueueAddress, connection, createMessageBodyColumn).ConfigureAwait(false);
                }
            }
        }

        static async Task CreateQueue(string creationScript, CanonicalQueueAddress canonicalQueueAddress, SqlConnection connection, bool createMessageBodyColumn)
        {
            try
            {
                using (var transaction = connection.BeginTransaction())
                {
                    var sql = string.Format(creationScript, canonicalQueueAddress.QualifiedTableName,
                        canonicalQueueAddress.QuotedCatalogName);
                    using (var command = new SqlCommand(sql, connection, transaction)
                    {
                        CommandType = CommandType.Text
                    })
                    {
                        await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                    }

                    transaction.Commit();
                }
            }
            catch (SqlException e) when (e.Number == 2714) //Object already exists
            {
                //Table creation scripts are based on sys.objects metadata views.
                //It looks that these views are not fully transactional and might
                //not return information on already created table under heavy load.
            }
            catch (Exception e) when (e.Message.StartsWith("There is already an object named") ||
                                      e.Message.StartsWith("The operation failed because an index or statistics with name"))
            {
                // ignored because of race when multiple endpoints start
            }

            if (createMessageBodyColumn)
            {
                var bodyStringSql = string.Format(SqlConstants.AddMessageBodyStringColumn,
                    canonicalQueueAddress.QualifiedTableName, canonicalQueueAddress.QuotedCatalogName);

                using (var transaction = connection.BeginTransaction())
                {
                    using (var command = new SqlCommand(bodyStringSql, connection, transaction)
                    {
                        CommandType = CommandType.Text
                    })
                    {
                        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }

                    transaction.Commit();
                }
            }
        }

        SqlConnectionFactory connectionFactory;
        QueueAddressTranslator addressTranslator;
        bool createMessageBodyColumn;
    }
}
#pragma warning restore 618