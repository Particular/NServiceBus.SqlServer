namespace NServiceBus.Transport.PostgreSql
{
    using System;
    using System.Data;
    using System.Data.Common;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using System.Threading;
    using Npgsql;
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
                    var tableName = canonicalQueueAddress.QualifiedTableName;

                    var sql = string.Format(creationScript, tableName);
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
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                //PostgreSQL error code 23505: unique_violation is returned
                //if the table creation is executed concurrently by multiple transactions
                //In this case we want to discard the exception and continue
            }

            if (createMessageBodyColumn)
            {
                var advisoryLockId = CalculateLockId(canonicalQueueAddress.QualifiedTableName);
                var bodyStringSql = string.Format(sqlConstants.AddMessageBodyStringColumn, canonicalQueueAddress.Schema, canonicalQueueAddress.Table, advisoryLockId);

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

        static long CalculateLockId(string text)
        {
            var byteContents = Encoding.Unicode.GetBytes(text);
            var hashText = SHA256.Create().ComputeHash(byteContents);

            long hashCodeStart = BitConverter.ToInt64(hashText, 0);
            long hashCodeMedium = BitConverter.ToInt64(hashText, 8);
            long hashCodeEnd = BitConverter.ToInt64(hashText, 24);

            return hashCodeStart ^ hashCodeMedium ^ hashCodeEnd;
        }

        ISqlConstants sqlConstants;
        DbConnectionFactory connectionFactory;
        Func<string, CanonicalQueueAddress> addressTranslator;
        bool createMessageBodyColumn;
    }
}