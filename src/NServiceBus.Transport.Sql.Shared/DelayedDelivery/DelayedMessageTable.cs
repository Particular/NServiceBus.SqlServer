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

    class DelayedMessageTable : IDelayedMessageStore
    {
        public DelayedMessageTable(ISqlConstants sqlConstants, string delayedQueueTable, string inputQueueTable)
        {
            storeCommand = string.Format(sqlConstants.StoreDelayedMessageText, delayedQueueTable);
            moveDueCommand = string.Format(sqlConstants.MoveDueDelayedMessageText, delayedQueueTable, inputQueueTable);
        }

        public event EventHandler<DateTime> OnStoreDelayedMessage;

        /// <summary>
        /// Stores the message into the delayed queue instead of input queue
        /// </summary>
        public async Task Store(OutgoingMessage message, TimeSpan dueAfter, string destination, DbConnection connection,
            DbTransaction transaction, CancellationToken cancellationToken = default)
        {
            var messageRow = StoreDelayedMessageCommand.From(message.Headers, message.Body, dueAfter, destination);
            using (var command = connection.CreateCommand())
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
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = moveDueCommand;
                command.CommandType = CommandType.Text;

                command.AddParameter("BatchSize", DbType.Int32, batchSize);
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        // No timeouts waiting
                        return new MoveDueResult.Moved(DateTime.MinValue);
                    }

                    var lockAcquired = reader.GetBoolean(2);
                    if (!lockAcquired)
                    {
                        return new MoveDueResult.LockNotAcquired();
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
            }
        }

        string storeCommand;
        string moveDueCommand;
    }
}