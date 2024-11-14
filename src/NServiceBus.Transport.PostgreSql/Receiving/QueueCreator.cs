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
            using var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false);
            foreach (var address in queueAddresses)
            {
                await CreateQueue(sqlConstants.CreateQueueText, addressTranslator(address), connection, createMessageBodyColumn, cancellationToken).ConfigureAwait(false);
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
                using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                var tableName = canonicalQueueAddress.QualifiedTableName;

                var sql = string.Format(creationScript, tableName, canonicalQueueAddress.Table);
                using (var command = connection.CreateCommand())
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
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                //PostgreSQL error code 23505: unique_violation is returned
                //if the table creation is executed concurrently by multiple transactions
                //In this case we want to discard the exception and continue
            }

            if (createMessageBodyColumn)
            {
                //Generates an int for the advisory lock identifier based on the qualified table name
                var advisoryLockId = CalculateLockId(canonicalQueueAddress.QualifiedTableName);
                var bodyStringSql = string.Format(sqlConstants.AddMessageBodyStringColumn, canonicalQueueAddress.Schema, canonicalQueueAddress.Table, advisoryLockId);

                using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = bodyStringSql;
                    command.CommandType = CommandType.Text;

                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        static long CalculateLockId(string text)
        {
            var byteContents = Encoding.Unicode.GetBytes(text);
            var hashText = SHA256.Create().ComputeHash(byteContents);

            //HINT: we assume that the first byte has the same collision probability as any other part of the hash
            return BitConverter.ToInt64(hashText, 0);
        }

        ISqlConstants sqlConstants;
        DbConnectionFactory connectionFactory;
        Func<string, CanonicalQueueAddress> addressTranslator;
        bool createMessageBodyColumn;
    }
}