namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Unicast.Queuing;
    using Logging;
    using Npgsql;
    using static System.String;

    class TableBasedQueue
    {
        public string Name { get; }

        public TableBasedQueue(string qualifiedTableName, string queueName, bool isStreamSupported)
        {
            this.qualifiedTableName = qualifiedTableName;
            Name = queueName;
            receiveCommand = Format(SqlConstants.ReceiveText, this.qualifiedTableName);
            purgeCommand = Format(SqlConstants.PurgeText, this.qualifiedTableName);
            cachedSendCommand = Format(SqlConstants.SendText, this.qualifiedTableName);
            purgeExpiredCommand = Format(SqlConstants.PurgeBatchOfExpiredMessagesText, this.qualifiedTableName);
            this.isStreamSupported = isStreamSupported;
        }

        public virtual async Task<int> TryPeek(NpgsqlConnection connection, NpgsqlTransaction transaction, int? timeoutInSeconds = null, CancellationToken cancellationToken = default)
        {
            using (var command = new NpgsqlCommand(peekCommand, connection, transaction)
            {
                CommandTimeout = timeoutInSeconds ?? 30
            })
            {
                var numberOfMessages = await command.ExecuteScalarAsyncOrDefault<int>(nameof(peekCommand), msg => log.Warn(msg), cancellationToken).ConfigureAwait(false);
                return numberOfMessages;
            }
        }

        public void FormatPeekCommand(int maxRecordsToPeek)
        {
            peekCommand = Format(SqlConstants.PeekText, qualifiedTableName, maxRecordsToPeek);
        }

        public virtual async Task<MessageReadResult> TryReceive(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
        {
            using (var command = new NpgsqlCommand(receiveCommand, connection, transaction))
            {
                return await ReadMessage(command, cancellationToken).ConfigureAwait(false);
            }
        }

        public Task DeadLetter(MessageRow poisonMessage, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
        {
            return SendRawMessage(poisonMessage, connection, transaction, cancellationToken);
        }

        public Task Send(OutgoingMessage message, TimeSpan timeToBeReceived, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
        {
            var messageRow = MessageRow.From(message.Headers, message.Body, timeToBeReceived);

            return SendRawMessage(messageRow, connection, transaction, cancellationToken);
        }

        async Task<MessageReadResult> ReadMessage(NpgsqlCommand command, CancellationToken cancellationToken)
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

                var readResult = await MessageRow.Read(dataReader, isStreamSupported, cancellationToken).ConfigureAwait(false);

                //HINT: Reading all pending results makes sure that any query execution error,
                //      sent after the first result, are thrown by the SqlDataReader as SqlExceptions.
                //      More details in: https://github.com/DapperLib/Dapper/issues/1210
                while (await dataReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                { }

                while (await dataReader.NextResultAsync(cancellationToken).ConfigureAwait(false))
                { }

                return readResult;
            }
        }

        async Task SendRawMessage(MessageRow message, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
        {
            try
            {
                using (var command = new NpgsqlCommand(cachedSendCommand, connection, transaction))
                {
                    message.PrepareSendCommand(command);

                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (NpgsqlException ex) when (ex.ErrorCode == 208)
            {
                ThrowQueueNotFoundException(ex);
            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                ThrowFailedToSendException(ex);
            }
        }

        void ThrowQueueNotFoundException(NpgsqlException ex)
        {
            throw new QueueNotFoundException(Name, $"Failed to send message to {qualifiedTableName}", ex);
        }

        void ThrowFailedToSendException(Exception ex)
        {
            throw new Exception($"Failed to send message to {qualifiedTableName}", ex);
        }

        public async Task<int> Purge(NpgsqlConnection connection, CancellationToken cancellationToken = default)
        {
            using (var command = new NpgsqlCommand(purgeCommand, connection))
            {
                return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<int> PurgeBatchOfExpiredMessages(NpgsqlConnection connection, int purgeBatchSize, CancellationToken cancellationToken = default)
        {
            using (var command = new NpgsqlCommand(purgeExpiredCommand, connection))
            {
                command.Parameters.AddWithValue("@BatchSize", purgeBatchSize);
                return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public override string ToString()
        {
            return qualifiedTableName;
        }

        string qualifiedTableName;
        string peekCommand;
        string receiveCommand;
        string cachedSendCommand;
        string purgeCommand;
        string purgeExpiredCommand;
        bool isStreamSupported;

        static readonly ILog log = LogManager.GetLogger<TableBasedQueue>();
    }
}