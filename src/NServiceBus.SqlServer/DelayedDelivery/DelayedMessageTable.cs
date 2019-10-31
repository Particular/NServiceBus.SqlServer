namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;
    using Transport;

    interface IDelayedMessageStore
    {
        Task Store(OutgoingMessage message, TimeSpan dueAfter, string destination, SqlConnection connection, SqlTransaction transaction);
    }

    class SendOnlyDelayedMessageStore : IDelayedMessageStore
    {
        public Task Store(OutgoingMessage message, TimeSpan dueAfter, string destination, SqlConnection connection, SqlTransaction transaction)
        {
            throw new Exception("Delayed delivery is only supported for non send-only endpoints.");
        }
    }

    class DelayedMessageTable : IDelayedMessageStore
    {
        public DelayedMessageTable(string delayedQueueTable, string inputQueueTable)
        {
#pragma warning disable 618
            storeCommand = string.Format(SqlConstants.StoreDelayedMessageText, delayedQueueTable);
            moveDueCommand = string.Format(SqlConstants.MoveDueDelayedMessageText, delayedQueueTable, inputQueueTable);
#pragma warning restore 618
        }

        public async Task Store(OutgoingMessage message, TimeSpan dueAfter, string destination, SqlConnection connection, SqlTransaction transaction)
        {
            var messageRow = StoreDelayedMessageCommand.From(message.Headers, message.Body, dueAfter, destination);
            using (var command = new SqlCommand(storeCommand, connection, transaction))
            {
                messageRow.PrepareSendCommand(command);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task MoveDueMessages(int batchSize, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
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