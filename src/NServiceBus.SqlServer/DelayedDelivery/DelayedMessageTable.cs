namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Data.Common;
    using System.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;
    using Transport;

    interface IDelayedMessageStore
    {
        Task Store(OutgoingMessage message, TimeSpan dueAfter, string destination, DbConnection connection, DbTransaction transaction);
    }

    class SendOnlyDelayedMessageStore : IDelayedMessageStore
    {
        public Task Store(OutgoingMessage message, TimeSpan dueAfter, string destination, DbConnection connection, DbTransaction transaction)
        {
            throw new Exception("Delayed delivery is not supported for send-only endpoints.");
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

        public async Task Store(OutgoingMessage message, TimeSpan dueAfter, string destination, DbConnection connection, DbTransaction transaction)
        {
            var messageRow = StoreDelayedMessageCommand.From(message.Headers, message.Body, dueAfter, destination);
            using (var command = connection.CreateCommand())
            {
                command.CommandText = storeCommand;
                command.Connection = connection;
                command.Transaction = transaction;
                
                messageRow.PrepareSendCommand(command);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task MoveDueMessages(int batchSize, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = moveDueCommand;
                command.Connection = connection;
                command.Transaction = transaction;

                var parameter = command.CreateParameter();
                parameter.ParameterName = "BatchSize";
                parameter.Value = batchSize;
                command.Parameters.Add(parameter);
                
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        string storeCommand;
        string moveDueCommand;
    }
}
