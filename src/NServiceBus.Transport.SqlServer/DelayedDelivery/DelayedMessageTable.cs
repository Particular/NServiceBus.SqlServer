namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Data;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;
    using Transport;

    interface IDelayedMessageStore
    {
        Task Store(OutgoingMessage message, TimeSpan dueAfter, string destination, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default);
    }

    class SendOnlyDelayedMessageStore : IDelayedMessageStore
    {
        public Task Store(OutgoingMessage message, TimeSpan dueAfter, string destination, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
        {
            throw new Exception("Delayed delivery is not supported for send-only endpoints.");
        }
    }

    class DelayedMessageTable : IDelayedMessageStore
    {
        public DelayedMessageTable(string delayedQueueTable, string inputQueueTable)
        {
            storeCommand = string.Format(SqlConstants.StoreDelayedMessageText, delayedQueueTable);
            moveDueCommand = string.Format(SqlConstants.MoveDueDelayedMessageText, delayedQueueTable, inputQueueTable);
        }

        public event EventHandler<DateTime> OnStoreDelayedMessage;

        public async Task Store(OutgoingMessage message, TimeSpan dueAfter, string destination, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
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

        /// <returns>The time of the next timeout due</returns>
        public async Task<DateTime> MoveDueMessages(int batchSize, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
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
                        return DateTime.UtcNow.AddMinutes(1);
                    }

                    // Normalizing in case of clock drift between executing machine and SQL Server instance
                    var sqlNow = reader.GetDateTime(0);
                    var sqlNextDue = reader.GetDateTime(1);
                    if (sqlNextDue <= sqlNow)
                    {
                        return DateTime.UtcNow;
                    }

                    return DateTime.UtcNow.Add(sqlNextDue - sqlNow);
                }
            }
        }

        string storeCommand;
        string moveDueCommand;
    }
}
