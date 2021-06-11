namespace NServiceBus.Transport.SqlServer
{
    using System;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Threading;
    using System.Threading.Tasks;
    using Transport;

    interface IDelayedMessageStore
    {
        Task Store(OutgoingMessage message, TimeSpan dueAfter, string destination, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken = default);
    }

    class SendOnlyDelayedMessageStore : IDelayedMessageStore
    {
        public Task Store(OutgoingMessage message, TimeSpan dueAfter, string destination, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken = default)
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

        public event EventHandler<DateTimeOffset> OnStoreDelayedMessage;

        public async Task Store(OutgoingMessage message, TimeSpan dueAfter, string destination, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken = default)
        {
            var messageRow = StoreDelayedMessageCommand.From(message.Headers, message.Body, dueAfter, destination);
            using (var command = new SqlCommand(storeCommand, connection, transaction))
            {
                messageRow.PrepareSendCommand(command);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            OnStoreDelayedMessage?.Invoke(null, DateTimeOffset.Now.Add(dueAfter));
        }

        /// <returns>The time of the next timeout due</returns>
        public async Task<DateTimeOffset> MoveDueMessages(int batchSize, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken = default)
        {
            using (var command = new SqlCommand(moveDueCommand, connection, transaction))
            {
                command.Parameters.AddWithValue("BatchSize", batchSize);
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        // No timeouts waiting
                        return DateTimeOffset.UtcNow.AddMinutes(1);
                    }

                    // Normalizing in case of clock drift between executing machine and SQL Server instance
                    var sqlNow = reader.GetDateTimeOffset(0);
                    var sqlNextDue = reader.GetDateTimeOffset(1);
                    if (sqlNextDue <= sqlNow)
                    {
                        return DateTimeOffset.UtcNow;
                    }

                    return DateTimeOffset.UtcNow.Add(sqlNextDue - sqlNow);
                }
            }
        }

        string storeCommand;
        string moveDueCommand;
    }
}
