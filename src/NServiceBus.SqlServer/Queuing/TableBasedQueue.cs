namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
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
            var messageData = ToMessageRow(message);

            return SendRawMessage(messageData, connection, transaction);
        }

        static MessageRow ToMessageRow(OutgoingMessage message)
        {
            return new MessageRow(
                Guid.NewGuid(),
                TryGetHeaderValue(message.Headers, Headers.CorrelationId, s => s),
                TryGetHeaderValue(message.Headers, Headers.ReplyToAddress, s => s),
                true,
                TryGetHeaderValue(message.Headers, Headers.TimeToBeReceived, s =>
                {
                    TimeSpan ttbr;
                    return TimeSpan.TryParse(s, out ttbr)
                    ? (int?)ttbr.TotalMilliseconds
                    : null;
                }),
                DictionarySerializer.Serialize(message.Headers),
                message.Body);
        }

        static T TryGetHeaderValue<T>(Dictionary<string, string> headers, string name, Func<string, T> conversion)
        {
            string text;
            if (!headers.TryGetValue(name, out text))
            {
                return default(T);
            }
            var value = conversion(text);
            return value;
        }

        public Task DeadLetterMessage(MessageRow poisonMessage, SqlConnection connection, SqlTransaction transaction)
        {
            return SendRawMessage(poisonMessage, connection, transaction);
        }

        async Task SendRawMessage(MessageRow message, SqlConnection connection, SqlTransaction transaction)
        {
            var commandText = string.Format(Sql.SendText, address.SchemaName, address.TableName);

            using (var command = new SqlCommand(commandText, connection, transaction))
            {
                message.PrepareSendCommand(command);
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