namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;

    class TableBasedQueue
    {
        public TableBasedQueue(QueueAddress address)
        {
            this.address = address;
        }

        public string TransportAddress => address.ToString();

        public virtual async Task<MessageReadResult> TryReceive(SqlConnection connection, SqlTransaction transaction)
        {
            //HINT: We do not have to escape schema and tableName. The are delimited identifiers in sql text.
            //      see: https://msdn.microsoft.com/en-us/library/ms175874.aspx
            var commandText = string.Format(Sql.ReceiveText, address.SchemaName, address.TableName);

            using (var command = new SqlCommand(commandText, connection, transaction))
            {
                return await ReadRawMessageData(command).ConfigureAwait(false);
            }
        }

        static async Task<MessageReadResult> ReadRawMessageData(SqlCommand command)
        {
            // We need sequential access to not buffer everything into memory
            using (var dataReader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow | CommandBehavior.SequentialAccess).ConfigureAwait(false))
            {
                if (await dataReader.ReadAsync().ConfigureAwait(false))
                {
                    return await MessageReadResultParser.ParseData(dataReader).ConfigureAwait(false);
                }

                return MessageReadResult.NoMessage;
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

        public virtual async Task<int> TryPeek(SqlConnection connection, CancellationToken token)
        {
            var commandText = string.Format(Sql.PeekText, address.SchemaName, address.TableName);
            // ReSharper disable once MethodSupportsCancellation
            // ExecuteReaderAsync throws InvalidOperationException instead of TaskCancelledException with localized exception message
            using (var command = new SqlCommand(commandText, connection))
            using (var dataReader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow).ConfigureAwait(false))
            {
                // ReSharper disable once MethodSupportsCancellation
                var numberOfMessages = (int) await command.ExecuteScalarAsync().ConfigureAwait(false);

                return numberOfMessages;
            }
        }

        public async Task<int> Purge(SqlConnection connection)
        {
            var commandText = string.Format(Sql.PurgeText, address.SchemaName, address.TableName);
            using (var command = new SqlCommand(commandText, connection))
            {
                return await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task<int> PurgeBatchOfExpiredMessages(SqlConnection connection, int purgeBatchSize)
        {
            var commandText = string.Format(Sql.PurgeBatchOfExpiredMessagesText, purgeBatchSize, address.SchemaName, address.TableName);
            using (var command = new SqlCommand(commandText, connection))
            {
                return await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task LogWarningWhenIndexIsMissing(SqlConnection connection)
        {
            var commandText = string.Format(Sql.CheckIfExpiresIndexIsPresent, Sql.ExpiresIndexName, address.SchemaName, address.TableName);

            using (var command = new SqlCommand(commandText, connection))
            {
                var rowsCount = (int) await command.ExecuteScalarAsync().ConfigureAwait(false);

                if (rowsCount == 0)
                {
                    Logger.WarnFormat(@"Table [{0}].[{1}] does not contain index '{2}'." + Environment.NewLine + "Adding this index will speed up the process of purging expired messages from the queue. Please consult the documentation for further information.", address.SchemaName, address.TableName, Sql.ExpiresIndexName);
                }
            }
        }

        public override string ToString()
        {
            return $"{address.SchemaName}.{address.TableName}";
        }

        QueueAddress address;

        static ILog Logger = LogManager.GetLogger(typeof(TableBasedQueue));
    }
}