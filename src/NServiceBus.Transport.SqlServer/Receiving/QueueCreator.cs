namespace NServiceBus.Transport.SqlServer
{
    using System.Data;
    using System.Data.Common;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Threading.Tasks;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;

    class QueueCreator
    {
        public QueueCreator(ISqlConstants sqlConstants, DbConnectionFactory connectionFactory, IQueueAddressTranslator addressTranslator, bool createMessageBodyColumn = false)
        {
            this.sqlConstants = sqlConstants;
            this.connectionFactory = connectionFactory;
            this.addressTranslator = addressTranslator;
            this.createMessageBodyColumn = createMessageBodyColumn;
        }

        public async Task CreateQueueIfNecessary(string[] queueAddresses, CanonicalQueueAddress delayedQueueAddress, CancellationToken cancellationToken = default)
        {
            using (var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
            {
                foreach (var address in queueAddresses)
                {
                    await CreateQueue(sqlConstants.CreateQueueText, addressTranslator.Parse(address), connection, createMessageBodyColumn, cancellationToken).ConfigureAwait(false);
                }

                if (delayedQueueAddress != null)
                {
                    await CreateQueue(sqlConstants.CreateDelayedMessageStoreText, delayedQueueAddress, connection, createMessageBodyColumn, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        async Task CreateQueue(string creationScript, CanonicalQueueAddress canonicalQueueAddress, DbConnection connection, bool createMessageBodyColumn, CancellationToken cancellationToken)
        {
            try
            {
                using (var transaction = connection.BeginTransaction())
                {
                    var sql = string.Format(creationScript, canonicalQueueAddress.QualifiedTableName,
                        canonicalQueueAddress.QuotedCatalogName);
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = sql;
                        command.CommandType = CommandType.Text;

                        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                    }

                    transaction.Commit();
                }
            }
            catch (SqlException e) when (e.Number is 2714 or 1913) //Object already exists
            {
                //Table creation scripts are based on sys.objects metadata views.
                //It looks that these views are not fully transactional and might
                //not return information on already created table under heavy load.
                //This in turn can result in executing table create or index create queries
                //for objects that already exists. These queries will fail with
                // 2714 (table) and 1913 (index) error codes.
            }

            if (createMessageBodyColumn)
            {
                var bodyStringSql = string.Format(sqlConstants.AddMessageBodyStringColumn,
                    canonicalQueueAddress.QualifiedTableName, canonicalQueueAddress.QuotedCatalogName);

                using (var transaction = connection.BeginTransaction())
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = bodyStringSql;
                        command.CommandType = CommandType.Text;

                        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }

                    transaction.Commit();
                }
            }
        }

        ISqlConstants sqlConstants;
        DbConnectionFactory connectionFactory;
        IQueueAddressTranslator addressTranslator;
        bool createMessageBodyColumn;
    }
}