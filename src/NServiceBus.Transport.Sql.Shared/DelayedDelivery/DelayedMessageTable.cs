namespace NServiceBus.Transport.Sql.Shared
{
    using System;
    using System.Data;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Transport;

    interface IDelayedMessageStore
    {
        Task Store(OutgoingMessage message, TimeSpan dueAfter, string destination, DbConnection connection,
            DbTransaction transaction, CancellationToken cancellationToken = default);
    }

    class DelayedMessageTable(ISqlConstants sqlConstants, string delayedQueueTable, IMoveDueDelayedMessagesCommand moveDueCommand) : IDelayedMessageStore
    {
        const int RetryLockAfterMsColumn = 2;

        public event EventHandler<DateTime> OnStoreDelayedMessage;

        /// <summary>
        /// Stores the message into the delayed queue instead of input queue
        /// </summary>
        public async Task Store(OutgoingMessage message, TimeSpan dueAfter, string destination, DbConnection connection,
            DbTransaction transaction, CancellationToken cancellationToken = default)
        {
            var messageRow = StoreDelayedMessageCommand.From(message.Headers, message.Body, dueAfter, destination);
            var command = connection.CreateCommand();
            await using (command.ConfigureAwait(false))
            {
                command.Transaction = transaction;
                command.CommandText = storeCommand;
                command.CommandType = CommandType.Text;

                messageRow.PrepareSendCommand(command);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            OnStoreDelayedMessage?.Invoke(null, DateTime.UtcNow.Add(dueAfter));
        }

        public async Task<MoveDueResult> MoveDueMessages(int batchSize, DbConnection connection, DbTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            var command = connection.CreateCommand();
            await using var commandDisposer = command.ConfigureAwait(false);
            command.Transaction = transaction;
            command.CommandType = CommandType.Text;
            moveDueCommand.Populate(command, batchSize);

            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using var readerDisposer = reader.ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                // No timeouts waiting
                return new MoveDueResult.Moved(DateTime.MinValue);
            }

            if (!await reader.IsDBNullAsync(RetryLockAfterMsColumn, cancellationToken).ConfigureAwait(false))
            {
                var retryLockAfter = TimeSpan.FromMilliseconds(reader.GetInt32(RetryLockAfterMsColumn));
                return new MoveDueResult.LockNotAcquired(retryLockAfter);
            }

            // Normalizing in case of clock drift between executing machine and database instance
            var sqlNow = reader.GetDateTime(0);
            var sqlNextDue = reader.GetDateTime(1);

            if (sqlNextDue <= sqlNow)
            {
                return new MoveDueResult.Moved(DateTime.UtcNow);
            }

            return new MoveDueResult.Moved(DateTime.UtcNow.Add(sqlNextDue - sqlNow));
        }

        readonly string storeCommand = string.Format(sqlConstants.StoreDelayedMessageText, delayedQueueTable);
    }
}
