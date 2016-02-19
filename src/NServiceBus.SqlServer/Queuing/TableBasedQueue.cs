namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    
    class TableBasedQueue
    {
        public TableBasedQueue(QueueAddress address)
        {
            this.address = address;
        }

        public async Task<MessageReadResult> TryReceive(SqlConnection connection, SqlTransaction transaction)
        {
            //HINT: We do not have to escape schema and tableName. The are delimited identifiers in sql text.
            //      see: https://msdn.microsoft.com/en-us/library/ms175874.aspx
            var commandText = string.Format(Sql.ReceiveText, address.SchemaName, address.TableName);

            using (var command = new SqlCommand(commandText, connection, transaction))
            {
                var rawMessageData = await ReadRawMessageData(command).ConfigureAwait(false);

                if (rawMessageData == null)
                {
                    return MessageReadResult.NoMessage;
                }

                try
                {
                    var message = MessageParser.ParseRawData(rawMessageData);

                    if (message.TTBRExpried(DateTime.UtcNow))
                    {
                        var messageId = message.GetLogicalId() ?? message.TransportId;

                        Logger.InfoFormat($"Message with ID={messageId} has expired. Removing it from queue.");

                        return MessageReadResult.NoMessage;
                    }

                    return MessageReadResult.Success(message);
                }
                catch (Exception ex)
                {
                    Logger.Error("Error receiving message. Probable message metadata corruption. Moving to error queue.", ex);

                    return MessageReadResult.Poison(rawMessageData);
                }
            }
        }

        static async Task<object[]> ReadRawMessageData(SqlCommand command)
        {
            using (var dataReader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow).ConfigureAwait(false))
            {
                if (await dataReader.ReadAsync().ConfigureAwait(false))
                {
                    var rowData = new object[dataReader.FieldCount];
                    dataReader.GetValues(rowData);

                    return rowData;
                }

                return null;
            }
        }

        public Task SendMessage(OutgoingMessage message, SqlConnection connection, SqlTransaction transaction)
        {
            var messageData = MessageParser.CreateRawMessageData(message);

            if (messageData.Length != Sql.Columns.All.Length)
            {
                throw new InvalidOperationException("The length of message data array must match the name of Parameters array.");
            }

            return SendRawMessage(messageData, connection, transaction);
        }

        public async Task SendRawMessage(object[] data, SqlConnection connection, SqlTransaction transaction)
        {
            var commandText = string.Format(Sql.SendText, address.SchemaName, address.TableName);

            using (var command = new SqlCommand(commandText, connection, transaction))
            {
                foreach (var column in Sql.Columns.All)
                {
                    command.Parameters.Add(column.Name, column.Type).Value = data[column.Index];
                }

                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task<int> TryPeek(SqlConnection connection, CancellationToken token)
        {
            var commandText = string.Format(Sql.PeekText, address.SchemaName, address.TableName);

            using (var command = new SqlCommand(commandText, connection))
            {
                // ReSharper disable once MethodSupportsCancellation
                // ExecuteReaderAsync throws InvalidOperationException instead of TaskCancelledException with localized exception message 
                using (var dataReader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow).ConfigureAwait(false))
                {
                    if (await dataReader.ReadAsync(token).ConfigureAwait(false))
                    {
                        var rowData = new object[1];
                        dataReader.GetValues(rowData);

                        return Convert.ToInt32(rowData[0]);
                    }

                    return 0;
                }
            }
        }

        public async Task<int> Purge(SqlConnection connection)
        {
            var commandText = string.Format(Sql.PurgeText, address.SchemaName, address.TableName);

            using (var command = new SqlCommand(commandText, connection))
            {
                var rowsCount = await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                return rowsCount;
            }
        }

        public string TransportAddress => address.ToString();

        QueueAddress address;

        static ILog Logger = LogManager.GetLogger(typeof(TableBasedQueue));
    }
}