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

        public TableBasedQueue(string qualifiedTableName, string queueName, bool isStreamSupported)
        {
            this.qualifiedTableName = qualifiedTableName;
            Name = queueName;
            receiveCommand = Format(SqlConstants.ReceiveText, this.qualifiedTableName);
            sendCommand = Format(SqlConstants.SendText, this.qualifiedTableName);
            purgeCommand = Format(SqlConstants.PurgeText, this.qualifiedTableName);
            purgeExpiredCommand = Format(SqlConstants.PurgeBatchOfExpiredMessagesText, this.qualifiedTableName);
            checkExpiresIndexCommand = Format(SqlConstants.CheckIfExpiresIndexIsPresent, this.qualifiedTableName);
            checkNonClusteredRowVersionIndexCommand = Format(SqlConstants.CheckIfNonClusteredRowVersionIndexIsPresent, this.qualifiedTableName);
            checkHeadersColumnTypeCommand = Format(SqlConstants.CheckHeadersColumnType, this.qualifiedTableName);
            this.isStreamSupported = isStreamSupported;
        }

        public virtual async Task<int> TryPeek(SqlConnection connection, SqlTransaction transaction, int? timeoutInSeconds = null, CancellationToken cancellationToken = default)
        {
            using (var command = new SqlCommand(peekCommand, connection, transaction)
            {
                CommandTimeout = timeoutInSeconds ?? 30
            })
            {
                var numberOfMessages = (int)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                return numberOfMessages;
            }
        }

        public void FormatPeekCommand(int maxRecordsToPeek)
        {
            peekCommand = Format(SqlConstants.PeekText, qualifiedTableName, maxRecordsToPeek);
        }

        public virtual async Task<MessageReadResult> TryReceive(SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken = default)
        {
            using (var command = new SqlCommand(receiveCommand, connection, transaction))
            {
                return await ReadMessage(command, cancellationToken).ConfigureAwait(false);
            }
        }

        public Task DeadLetter(MessageRow poisonMessage, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken = default)
        {
            return SendRawMessage(poisonMessage, connection, transaction, cancellationToken);
        }

        public Task Send(OutgoingMessage message, TimeSpan timeToBeReceived, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken = default)
        {
            var messageRow = MessageRow.From(message.Headers, message.Body, timeToBeReceived);

            return SendRawMessage(messageRow, connection, transaction, cancellationToken);
        }

        async Task<MessageReadResult> ReadMessage(SqlCommand command, CancellationToken cancellationToken)
        {
            var behavior = CommandBehavior.SingleRow;
            if (isStreamSupported)
            {
                behavior |= CommandBehavior.SequentialAccess;
            }

            using (var dataReader = await command.ExecuteReaderAsync(behavior, cancellationToken).ConfigureAwait(false))
            {
                if (!await dataReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return MessageReadResult.NoMessage;
                }

                return await MessageRow.Read(dataReader, isStreamSupported, cancellationToken).ConfigureAwait(false);
            }
        }

        async Task SendRawMessage(MessageRow message, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            try
            {
                using (var command = new SqlCommand(sendCommand, connection, transaction))
                {
                    message.PrepareSendCommand(command);

                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                ThrowQueueNotFoundException(ex);
            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
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

        public async Task<int> Purge(SqlConnection connection, CancellationToken cancellationToken = default)
        {
            using (var command = new SqlCommand(purgeCommand, connection))
            {
                return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<int> PurgeBatchOfExpiredMessages(SqlConnection connection, int purgeBatchSize, CancellationToken cancellationToken = default)
        {
            using (var command = new SqlCommand(purgeExpiredCommand, connection))
            {
                command.Parameters.AddWithValue("@BatchSize", purgeBatchSize);
                return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<bool> CheckExpiresIndexPresence(SqlConnection connection, CancellationToken cancellationToken = default)
        {
            using (var command = new SqlCommand(checkExpiresIndexCommand, connection))
            {
                var rowsCount = (int)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                return rowsCount > 0;
            }
        }

        public async Task<bool> CheckNonClusteredRowVersionIndexPresence(SqlConnection connection, CancellationToken cancellationToken = default)
        {
            using (var command = new SqlCommand(checkNonClusteredRowVersionIndexCommand, connection))
            {
                var rowsCount = (int)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                return rowsCount > 0;
            }
        }

        public async Task<string> CheckHeadersColumnType(SqlConnection connection, CancellationToken cancellationToken = default)
        {
            using (var command = new SqlCommand(checkHeadersColumnTypeCommand, connection))
            {
                return (string)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
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
        bool isStreamSupported;
    }
}