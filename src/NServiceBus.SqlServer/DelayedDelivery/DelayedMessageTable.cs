namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Transport;

    class DelayedMessageTable
    {
        public DelayedMessageTable(string inputQueue, string delayedMessageTableSuffix, QueueAddressParser addressParser)
        {
            using (var sanitizer = new SqlCommandBuilder())
            {
                var inputQueueAddress = addressParser.Parse(inputQueue);

                inputQueueTable = sanitizer.QuoteIdentifier(inputQueueAddress.TableName);
                schema = sanitizer.QuoteIdentifier(inputQueueAddress.SchemaName);

                delayedStoreTable = sanitizer.QuoteIdentifier(inputQueueAddress.TableName + "." + delayedMessageTableSuffix);
            }
        }

        public async Task Store(OutgoingMessage message, DateTime dueTime, string destination, SqlConnection connection, SqlTransaction transaction)
        {
            var messageRow = DelayedMessageRow.From(message.Headers, message.Body, dueTime, destination);

            var commandText = string.Format(Sql.StoreDelayedMessageText, schema, delayedStoreTable);

            try
            {
                using (var command = new SqlCommand(commandText, connection, transaction))
                {
                    messageRow.PrepareSendCommand(command);
                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to store a delayed message.", ex);
            }
        }

        public Task MoveMaturedMessages(SqlConnection connection, SqlTransaction transaction)
        {
            var commandText = string.Format(Sql.MoveMaturedDelayedMessageText, schema, delayedStoreTable, inputQueueTable);

            using (var command = new SqlCommand(commandText, connection, transaction))
            {
                return command.ExecuteNonQueryAsync();
            }
        }

        public async Task<int> Purge(SqlConnection connection)
        {
            var commandText = string.Format(Sql.PurgeText, schema, delayedStoreTable);

            using (var command = new SqlCommand(commandText, connection))
            {
                return await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }
        
        string delayedStoreTable;
        string inputQueueTable;

        string schema;
    }
}