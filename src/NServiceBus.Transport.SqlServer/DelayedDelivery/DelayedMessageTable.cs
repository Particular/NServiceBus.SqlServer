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

        public async Task Store(OutgoingMessage message, TimeSpan dueAfter, string destination, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken = default)
        {
            var messageRow = StoreDelayedMessageCommand.From(message.Headers, message.Body, dueAfter, destination);
            using (var command = new SqlCommand(storeCommand, connection, transaction))
            {
                messageRow.PrepareSendCommand(command);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task MoveDueMessages(int batchSize, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken = default)
        {
            using (var command = new SqlCommand(moveDueCommand, connection, transaction))
            {
                command.Parameters.AddWithValue("BatchSize", batchSize);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        string storeCommand;
        string moveDueCommand;
    }
}
