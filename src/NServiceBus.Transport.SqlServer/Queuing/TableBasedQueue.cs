namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Data;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Threading;
    using System.Threading.Tasks;
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
            receiveCommand = Format(SqlConstants.ReceiveText, this.qualifiedTableName);
            sendCommand = Format(SqlConstants.SendText, this.qualifiedTableName);
            purgeCommand = Format(SqlConstants.PurgeText, this.qualifiedTableName);
            purgeExpiredCommand = Format(SqlConstants.PurgeBatchOfExpiredMessagesText, this.qualifiedTableName);
            checkExpiresIndexCommand = Format(SqlConstants.CheckIfExpiresIndexIsPresent, this.qualifiedTableName);
            checkNonClusteredRowVersionIndexCommand = Format(SqlConstants.CheckIfNonClusteredRowVersionIndexIsPresent, this.qualifiedTableName);
            checkHeadersColumnTypeCommand = Format(SqlConstants.CheckHeadersColumnType, this.qualifiedTableName);
#pragma warning restore 618
        }

        public virtual async Task<int> TryPeek(SqlConnection connection, SqlTransaction transaction, CancellationToken token, int timeoutInSeconds = 30)
        {
            using (var command = new SqlCommand(peekCommand, connection, transaction)
            {
                CommandTimeout = timeoutInSeconds
            })
            {
                var numberOfMessages = (int)await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                return numberOfMessages;
            }
        }

        public void FormatPeekCommand(int maxRecordsToPeek)
        {
#pragma warning disable 618
            peekCommand = Format(SqlConstants.PeekText, qualifiedTableName, maxRecordsToPeek);
#pragma warning restore 618
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

        public Task Send(OutgoingMessage message, TimeSpan timeToBeReceived, SqlConnection connection, SqlTransaction transaction)
        {
            var messageRow = MessageRow.From(message.Headers, message.Body, timeToBeReceived);

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

                return await MessageRow.Read(dataReader).ConfigureAwait(false);
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

        public async Task<bool> CheckExpiresIndexPresence(SqlConnection connection)
        {
            using (var command = new SqlCommand(checkExpiresIndexCommand, connection))
            {
                var rowsCount = (int)await command.ExecuteScalarAsync().ConfigureAwait(false);
                return rowsCount > 0;
            }
        }

        public async Task<bool> CheckNonClusteredRowVersionIndexPresence(SqlConnection connection)
        {
            using (var command = new SqlCommand(checkNonClusteredRowVersionIndexCommand, connection))
            {
                var rowsCount = (int)await command.ExecuteScalarAsync().ConfigureAwait(false);
                return rowsCount > 0;
            }
        }

        public async Task<string> CheckHeadersColumnType(SqlConnection connection)
        {
            using (var command = new SqlCommand(checkHeadersColumnTypeCommand, connection))
            {
                return (string)await command.ExecuteScalarAsync().ConfigureAwait(false);
            }
        }

        public override string ToString()
        {
            return qualifiedTableName;
        }

        string qualifiedTableName;
        string peekCommand;
        string receiveCommand;
        string sendCommand;
        string purgeCommand;
        string purgeExpiredCommand;
        string checkExpiresIndexCommand;
        string checkNonClusteredRowVersionIndexCommand;
        string checkHeadersColumnTypeCommand;
    }
}