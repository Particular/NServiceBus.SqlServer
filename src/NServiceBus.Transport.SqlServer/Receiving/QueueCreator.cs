namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Data;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Transport.Sql.Shared;

    class QueueCreator(ISqlConstants sqlConstants, string createQueueScript, DbConnectionFactory connectionFactory, Func<string, CanonicalQueueAddress> addressTranslator, bool createMessageBodyColumn = false)
    {
        public async Task CreateQueueIfNecessary(string[] queueAddresses, CanonicalQueueAddress delayedQueueAddress, CancellationToken cancellationToken = default)
        {
            var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false);
            await using var connectionDisposer = connection.ConfigureAwait(false);
            foreach (var address in queueAddresses)
            {
                await CreateQueue(createQueueScript, addressTranslator(address), connection, createMessageBodyColumn, cancellationToken).ConfigureAwait(false);
            }

            if (delayedQueueAddress != null)
            {
                await CreateQueue(sqlConstants.CreateDelayedMessageStoreText, delayedQueueAddress, connection, createMessageBodyColumn, cancellationToken).ConfigureAwait(false);
            }
        }

        async Task CreateQueue(string creationScript, CanonicalQueueAddress canonicalQueueAddress, DbConnection connection, bool createMessageBodyColumn, CancellationToken cancellationToken)
        {
            try
            {
                var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                await using var transactionDisposer = transaction.ConfigureAwait(false);
                var sql = string.Format(creationScript, canonicalQueueAddress.QualifiedTableName,
                    canonicalQueueAddress.QuotedCatalogName);
                var command = connection.CreateCommand();
                await using (command.ConfigureAwait(false))
                {
                    command.Transaction = transaction;
                    command.CommandText = sql;
                    command.CommandType = CommandType.Text;

                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
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

                var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                await using var transactionDisposer = transaction.ConfigureAwait(false);
                var command = connection.CreateCommand();
                await using (command.ConfigureAwait(false))
                {
                    command.Transaction = transaction;
                    command.CommandText = bodyStringSql;
                    command.CommandType = CommandType.Text;

                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}