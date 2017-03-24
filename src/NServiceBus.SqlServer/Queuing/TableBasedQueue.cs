namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using Unicast.Queuing;
    using static System.String;

    class TableBasedQueue
    {
        public string Name { get; }

        public TableBasedQueue(string qualifiedTableName, string queueName)
        {
            this.qualifiedTableName = qualifiedTableName;
            Name = queueName;
        }

        public virtual async Task<int> TryPeek(SqlConnection connection, CancellationToken token, int timeoutInSeconds = 30)
        {
            var commandText = Format(Sql.PeekText, qualifiedTableName);

            using (var command = new SqlCommand(commandText, connection)
            {
                CommandTimeout = timeoutInSeconds
            })
            {
                var numberOfMessages = (int) await command.ExecuteScalarAsync(token).ConfigureAwait(false);

                return numberOfMessages;
            }
        }

        public virtual async Task<MessageReadResult> TryReceive(SqlConnection connection, SqlTransaction transaction)
        {
            var commandText = Format(Sql.ReceiveText, qualifiedTableName);

            using (var command = new SqlCommand(commandText, connection, transaction))
            {
                return await ReadMessage(command).ConfigureAwait(false);
            }
        }

        public Task DeadLetter(MessageRow poisonMessage, SqlConnection connection, SqlTransaction transaction)
        {
            return SendRawMessage(poisonMessage, connection, transaction);
        }

        public Task Send(Dictionary<string, string> headers, byte[] body, SqlConnection connection, SqlTransaction transaction)
        {
            var messageRow = MessageRow.From(headers, body);

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
            var commandText = Format(Sql.SendText, qualifiedTableName);

            try
            {
                using (var command = new SqlCommand(commandText, connection, transaction))
                {
                    message.PrepareSendCommand(command);

                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 208)
                {
                    ThrowQueueNotFoundException(ex);
                }

                ThrowFailedToSendException(ex);
            }
            catch (Exception ex)
            {
                ThrowFailedToSendException(ex);
            }
        }

        void ThrowQueueNotFoundException(SqlException ex)
        {
            throw new QueueNotFoundException(Name, $"Failed to send message to {qualifiedTableName}", ex);
        }

        void ThrowFailedToSendException(Exception ex)
        {
            throw new Exception($"Failed to send message to {qualifiedTableName}", ex);
        }

        public async Task<int> Purge(SqlConnection connection)
        {
            var commandText = Format(Sql.PurgeText, qualifiedTableName);

            using (var command = new SqlCommand(commandText, connection))
            {
                return await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task<int> PurgeBatchOfExpiredMessages(SqlConnection connection, int purgeBatchSize)
        {
            var commandText = Format(Sql.PurgeBatchOfExpiredMessagesText, purgeBatchSize, qualifiedTableName);

            using (var command = new SqlCommand(commandText, connection))
            {
                return await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task LogWarningWhenIndexIsMissing(SqlConnection connection)
        {
            var qualifiedTableName1 = qualifiedTableName;
            var commandText = Format(Sql.CheckIfExpiresIndexIsPresent, Sql.ExpiresIndexName, qualifiedTableName1);

            using (var command = new SqlCommand(commandText, connection))
            {
                var rowsCount = (int) await command.ExecuteScalarAsync().ConfigureAwait(false);

                if (rowsCount == 0)
                {
                    Logger.Warn(Format(@"Table {0} does not contain index '{1}'." + Environment.NewLine + "Adding this index will speed up the process of purging expired messages from the queue. Please consult the documentation for further information.", qualifiedTableName1, Sql.ExpiresIndexName));
                }
            }
        }

        public override string ToString()
        {
            return qualifiedTableName;
        }

        static ILog Logger = LogManager.GetLogger(typeof(TableBasedQueue));
        string qualifiedTableName;
    }
}