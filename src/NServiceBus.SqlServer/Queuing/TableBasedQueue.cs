namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using static System.String;

    class TableBasedQueue
    {
        public TableBasedQueue(QueueAddress address)
        {
            this.address = address;
        }

        public string TransportAddress => address.ToString();

        public virtual async Task<int> TryPeek(SqlConnection connection, CancellationToken token)
        {
            var commandText = Format(Sql.PeekText, address.SchemaName, address.TableName);

            using (var command = new SqlCommand(commandText, connection))
            {
                var numberOfMessages = (int)await command.ExecuteScalarAsync(token).ConfigureAwait(false);

                return numberOfMessages;
            }
        }

        public virtual async Task<MessageReadResult> TryReceive(SqlConnection connection, SqlTransaction transaction)
        {
            //HINT: We do not have to escape schema and tableName. The are delimited identifiers in sql text.
            //      see: https://msdn.microsoft.com/en-us/library/ms175874.aspx
            var commandText = Format(Sql.ReceiveText, address.SchemaName, address.TableName);

            using (var command = new SqlCommand(commandText, connection, transaction))
            {
                return await ReadMessage(command).ConfigureAwait(false);
            }
        }

        public Task DeadLetter(MessageRow poisonMessage, SqlConnection connection, SqlTransaction transaction)
        {
            return SendRawMessage(poisonMessage, connection, transaction);
        }

        public Task Send(OutgoingMessage message, SqlConnection connection, SqlTransaction transaction)
        {
            var messageRow = MessageRow.From(message.Headers, message.Body);

            return SendRawMessage(messageRow, connection, transaction);
        }

        static async Task<MessageReadResult> ReadMessage(SqlCommand command)
        {
            // We need sequential access to not buffer everything into memory
            using (var dataReader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow | CommandBehavior.SequentialAccess).ConfigureAwait(false))
            {
                if (!await dataReader.ReadAsync().ConfigureAwait(false))
                {
                    return MessageReadResult.NoMessage;
                }

                var readResult = await MessageRow.Read(dataReader).ConfigureAwait(false);

                return readResult;
            }
        }

        async Task SendRawMessage(MessageRow message, SqlConnection connection, SqlTransaction transaction)
        {
            var commandText = Format(Sql.SendText, address.SchemaName, address.TableName);

            using (var command = new SqlCommand(commandText, connection, transaction))
            {
                message.PrepareSendCommand(command);

                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task<int> Purge(SqlConnection connection)
        {
            var commandText = Format(Sql.PurgeText, address.SchemaName, address.TableName);

            using (var command = new SqlCommand(commandText, connection))
            {
                return await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task<int> PurgeBatchOfExpiredMessages(SqlConnection connection, int purgeBatchSize)
        {
            var commandText = Format(Sql.PurgeBatchOfExpiredMessagesText, purgeBatchSize, address.SchemaName, address.TableName);

            using (var command = new SqlCommand(commandText, connection))
            {
                return await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task LogWarningWhenIndexIsMissing(SqlConnection connection)
        {
            var commandText = Format(Sql.CheckIfExpiresIndexIsPresent, Sql.ExpiresIndexName, address.SchemaName, address.TableName);

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