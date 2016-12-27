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
#pragma warning disable 618
            this.qualifiedTableName = qualifiedTableName;
            Name = queueName;
            peekCommand = Format(SqlConstants.PeekText, this.qualifiedTableName);
            receiveCommand = Format(SqlConstants.ReceiveText, this.qualifiedTableName);
            sendCommand = Format(SqlConstants.SendText, this.qualifiedTableName);
            purgeCommand = Format(SqlConstants.PurgeText, this.qualifiedTableName);
            purgeExpiredCommand = Format(SqlConstants.PurgeBatchOfExpiredMessagesText, this.qualifiedTableName);
            checkIndexCommand = Format(SqlConstants.CheckIfExpiresIndexIsPresent, this.qualifiedTableName);
#pragma warning restore 618
        }

        public virtual async Task<int> TryPeek(SqlConnection connection, CancellationToken token, int timeoutInSeconds = 30)
        {
            using (var command = new SqlCommand(peekCommand, connection)
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
            using (var command = new SqlCommand(receiveCommand, connection, transaction))
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
            try
            {
                using (var command = new SqlCommand(sendCommand, connection, transaction))
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
            using (var command = new SqlCommand(purgeCommand, connection))
            {
                return await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task<int> PurgeBatchOfExpiredMessages(SqlConnection connection, int purgeBatchSize)
        {
            using (var command = new SqlCommand(purgeExpiredCommand, connection))
            {
                command.Parameters.AddWithValue("@BatchSize", purgeBatchSize);
                return await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task LogWarningWhenIndexIsMissing(SqlConnection connection)
        {
            using (var command = new SqlCommand(checkIndexCommand, connection))
            {
                var rowsCount = (int) await command.ExecuteScalarAsync().ConfigureAwait(false);

                if (rowsCount == 0)
                {
                    Logger.Warn($@"Table {qualifiedTableName} does not contain index 'Index_Expires'.{Environment.NewLine}Adding this index will speed up the process of purging expired messages from the queue. Please consult the documentation for further information.");
                }
            }
        }

        public override string ToString()
        {
            return qualifiedTableName;
        }

        static ILog Logger = LogManager.GetLogger(typeof(TableBasedQueue));
        string qualifiedTableName;
        string peekCommand;
        string receiveCommand;
        string sendCommand;
        string purgeCommand;
        string purgeExpiredCommand;
        string checkIndexCommand;
    }
}