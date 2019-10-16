namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;
    using Transport;

    class DelayedMessageTable
    {
        public DelayedMessageTable(string delayedQueueTable, string inputQueueTable)
        {
#pragma warning disable 618
            storeCommand = string.Format(SqlConstants.StoreDelayedMessageText, delayedQueueTable);
            moveMaturedCommand = string.Format(SqlConstants.MoveMaturedDelayedMessageText, delayedQueueTable, inputQueueTable);
#pragma warning restore 618
        }

        public async Task Store(OutgoingMessage message, DateTime dueTime, string destination, SqlConnection connection, SqlTransaction transaction)
        {
            var messageRow = DelayedMessageRow.From(message.Headers, message.Body, dueTime, destination);
            using (var command = new SqlCommand(storeCommand, connection, transaction))
            {
                messageRow.PrepareSendCommand(command);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task MoveMaturedMessages(int batchSize, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            using (var command = new SqlCommand(moveMaturedCommand, connection, transaction))
            {
                command.Parameters.AddWithValue("BatchSize", batchSize);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        string storeCommand;
        string moveMaturedCommand;
    }
}