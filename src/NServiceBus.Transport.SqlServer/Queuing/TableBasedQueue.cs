namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Data;
    using System.Data.Common;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using Microsoft.Data.SqlClient;
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
            purgeCommand = Format(SqlConstants.PurgeText, this.qualifiedTableName);
            purgeExpiredCommand = Format(SqlConstants.PurgeBatchOfExpiredMessagesText, this.qualifiedTableName);
            checkExpiresIndexCommand = Format(SqlConstants.CheckIfExpiresIndexIsPresent, this.qualifiedTableName);
            checkNonClusteredRowVersionIndexCommand = Format(SqlConstants.CheckIfNonClusteredRowVersionIndexIsPresent, this.qualifiedTableName);
            checkHeadersColumnTypeCommand = Format(SqlConstants.CheckHeadersColumnType, this.qualifiedTableName);
            this.isStreamSupported = isStreamSupported;
        }

        public virtual async Task<int> TryPeek(DbConnection connection, DbTransaction transaction, int? timeoutInSeconds = null, CancellationToken cancellationToken = default)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandTimeout = timeoutInSeconds ?? 30;
                command.CommandType = CommandType.Text;
                command.Transaction = transaction;
                command.CommandText = peekCommand;

                var numberOfMessages = await command.ExecuteScalarAsyncOrDefault<int>(nameof(peekCommand), msg => log.Warn(msg), cancellationToken).ConfigureAwait(false);
                return numberOfMessages;
            }
        }

        public void FormatPeekCommand(int maxRecordsToPeek)
        {
            peekCommand = Format(SqlConstants.PeekText, qualifiedTableName, maxRecordsToPeek);
        }

        public virtual async Task<MessageReadResult> TryReceive(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = receiveCommand;
                command.Transaction = transaction;
                command.CommandType = CommandType.Text;

                return await ReadMessage(command, cancellationToken).ConfigureAwait(false);
            }
        }

        public Task DeadLetter(MessageRow poisonMessage, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
        {
            return SendRawMessage(poisonMessage, connection, transaction, cancellationToken);
        }

        public Task Send(OutgoingMessage message, TimeSpan timeToBeReceived, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
        {
            var messageRow = MessageRow.From(message.Headers, message.Body, timeToBeReceived);

            return SendRawMessage(messageRow, connection, transaction, cancellationToken);
        }

        async Task<MessageReadResult> ReadMessage(DbCommand command, CancellationToken cancellationToken)
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

        async Task SendRawMessage(MessageRow message, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
        {
            try
            {
                var sendCommand = await GetSendCommandText(connection, transaction, cancellationToken).ConfigureAwait(false);

                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = sendCommand;
                    command.Transaction = transaction;

                    message.PrepareSendCommand(command);

                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            // 207 = Invalid column name
            // 515 = Cannot insert the value NULL into column; column does not allow nulls
            catch (SqlException ex) when ((ex.Number == 207 || ex.Number == 515) && ex.Message.Contains("Recoverable"))
            {
                cachedSendCommand = null;
                throw new Exception($"Failed to send message to {qualifiedTableName} due to a change in the existence of the Recoverable column that is scheduled for removal. If the table schema has changed, this is expected. Retrying the message send will detect the new table structure and adapt to it.", ex);
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

        async Task<string> GetSendCommandText(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
        {
            // Get a local reference because another thread could remove the cache between check and return
            var sendCommand = cachedSendCommand;
            if (sendCommand != null)
            {
                return sendCommand;
            }

            await sendCommandLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                sendCommand = cachedSendCommand;
                if (sendCommand != null)
                {
                    return sendCommand;
                }

                var commandText = Format(SqlConstants.CheckIfTableHasRecoverableText, qualifiedTableName);
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = commandText;
                    command.Transaction = transaction;

                    using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                    {
                        for (int fieldIndex = 0; fieldIndex < reader.FieldCount; fieldIndex++)
                        {
                            if (string.Equals("Recoverable", reader.GetName(fieldIndex), StringComparison.OrdinalIgnoreCase))
                            {
                                cachedSendCommand = Format(SqlConstants.SendTextWithRecoverable, qualifiedTableName);
                                return cachedSendCommand;
                            }
                        }
                    }

                    cachedSendCommand = Format(SqlConstants.SendText, qualifiedTableName);
                    return cachedSendCommand;
                }
            }
            finally
            {
                sendCommandLock.Release();
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

        public async Task<int> Purge(DbConnection connection, CancellationToken cancellationToken = default)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = purgeCommand;
                command.CommandType = CommandType.Text;

                return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<int> PurgeBatchOfExpiredMessages(DbConnection connection, int purgeBatchSize, CancellationToken cancellationToken = default)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = purgeExpiredCommand;
                command.CommandType = CommandType.Text;
                command.AddParameter("@BatchSize", DbType.Int32, purgeBatchSize);

                return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<bool> CheckExpiresIndexPresence(DbConnection connection, CancellationToken cancellationToken = default)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = checkExpiresIndexCommand;
                command.CommandType = CommandType.Text;

                var rowsCount = await command.ExecuteScalarAsync<int>(nameof(checkExpiresIndexCommand), cancellationToken).ConfigureAwait(false);
                return rowsCount > 0;
            }
        }

        public async Task<bool> CheckNonClusteredRowVersionIndexPresence(DbConnection connection, CancellationToken cancellationToken = default)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = checkNonClusteredRowVersionIndexCommand;
                command.CommandType = CommandType.Text;

                var rowsCount = await command.ExecuteScalarAsync<int>(nameof(checkNonClusteredRowVersionIndexCommand), cancellationToken).ConfigureAwait(false);
                return rowsCount > 0;
            }
        }

        public async Task<string> CheckHeadersColumnType(DbConnection connection, CancellationToken cancellationToken = default)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = checkHeadersColumnTypeCommand;
                command.CommandType = CommandType.Text;

                return await command.ExecuteScalarAsync<string>(nameof(checkHeadersColumnTypeCommand), cancellationToken).ConfigureAwait(false);
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
        string checkExpiresIndexCommand;
        string checkNonClusteredRowVersionIndexCommand;
        string checkHeadersColumnTypeCommand;
        bool isStreamSupported;

        readonly SemaphoreSlim sendCommandLock = new SemaphoreSlim(1, 1);
        static readonly ILog log = LogManager.GetLogger<TableBasedQueue>();
    }
}