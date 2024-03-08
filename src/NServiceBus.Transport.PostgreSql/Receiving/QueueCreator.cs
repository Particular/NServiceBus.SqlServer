namespace NServiceBus.Transport.PostgreSql
{
    using System;
    using System.Data;
    using System.Data.Common;
    using System.Threading.Tasks;
    using System.Threading;
    using Sql.Shared.Configuration;
    using Sql.Shared.Queuing;

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
                    var sql = string.Format(creationScript, canonicalQueueAddress.QualifiedTableName);
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

            if (createMessageBodyColumn)
            {
                var bodyStringSql = string.Format(sqlConstants.AddMessageBodyStringColumn, canonicalQueueAddress.QualifiedTableName);

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