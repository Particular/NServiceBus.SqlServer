namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Data;
    using System.Data.Common;
    using System.Threading.Tasks;
    using System.Threading;

    class QueueCreator
    {
        public QueueCreator(ISqlConstants sqlConstants, DbConnectionFactory connectionFactory, Func<string, CanonicalQueueAddress> addressTranslator, bool createMessageBodyColumn = false)
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
                    await CreateQueue(sqlConstants.CreateQueueText, addressTranslator(address), connection, createMessageBodyColumn, cancellationToken).ConfigureAwait(false);
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
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex) when (ex.IsObjectAlreadyExists())
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
        Func<string, CanonicalQueueAddress> addressTranslator;
        bool createMessageBodyColumn;
    }
}