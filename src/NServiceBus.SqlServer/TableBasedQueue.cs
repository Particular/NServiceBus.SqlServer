namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using NServiceBus.Logging;
    
    class TableBasedQueue
    {
        public TableBasedQueue(string queueName, string schema)
        {
            this.tableName = queueName;
            this.schema = schema;
        }

        public MessageReadResult TryReceive(SqlConnection connection, SqlTransaction transaction)
        {
            using (var command = new SqlCommand(String.Format(Sql.ReceiveText, this.schema, this.tableName), connection, transaction))
            {
                var rawMessageData = ReadRawMessageData(command);

                if (rawMessageData == null)
                {
                    return MessageReadResult.NoMessage;
                }

                try
                {
                    var message = SqlMessageParser.ParseRawData(rawMessageData);

                    if (message.TimeToBeReceived.HasValue && message.TimeToBeReceived.Value < DateTime.UtcNow)
                    {
                        //TODO: do we what to have message id from the header?
                        Logger.InfoFormat("Message with ID={0} has expired. Removing it from queue.", message.TransportId);

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

        object[] ReadRawMessageData(SqlCommand command)
        {
            using (var dataReader = command.ExecuteReader(CommandBehavior.SingleRow))
            {
                if (dataReader.Read())
                {
                    var rowData = new object[dataReader.FieldCount];
                    dataReader.GetValues(rowData);

                    return rowData;
                }

                return null;
            }
        }

        public void SendMessage(OutgoingMessage message, SqlConnection connection, SqlTransaction transaction)
        {
            var messageData = SqlMessageParser.CreateRawMessageData(message);

            if (messageData.Length != Sql.Columns.All.Length)
            {
                throw new InvalidOperationException("The length of message data array must match the name of Parameters array.");
            }

            SendRawMessage(messageData, connection, transaction);
        }

        public void SendRawMessage(object[] data, SqlConnection connection, SqlTransaction transaction)
        {
            var commandText = String.Format(Sql.SendText, this.schema, this.tableName);

            using (var command = new SqlCommand(commandText, connection, transaction))
            {
                foreach (var column in Sql.Columns.All)
                {
                    command.Parameters.Add(column.Name, column.Type).Value = data[column.Index];
                }

                command.ExecuteNonQuery();
            }
        }

        public bool TryPeek(SqlConnection connection, out int messageCount)
        {
            var commandText = String.Format(Sql.PeekText, this.schema, this.tableName);

            using (var command = new SqlCommand(commandText, connection))
            {
                using (var dataReader = command.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (dataReader.Read())
                    {
                        var rowData = new object[1];
                        dataReader.GetValues(rowData);

                        messageCount = Convert.ToInt32(rowData[0]);
                        return true;
                    }

                    messageCount = 0;
                    return false;
                }
            }
        }

        public int Purge(SqlConnection connection)
        {
            var commandText = string.Format(Sql.PurgeText, this.schema, this.tableName);

            using (var command = new SqlCommand(commandText, connection))
            {
                return command.ExecuteNonQuery();
            }
        }

        public override string ToString()
        {
            return tableName;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(TableBasedQueue));

        readonly string tableName;
        readonly string schema;
    }
}