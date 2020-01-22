namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Data;
    using System.Data.Common;
    using System.Data.SqlClient;
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
            checkIndexCommand = Format(SqlConstants.CheckIfExpiresIndexIsPresent, this.qualifiedTableName);
            checkHeadersColumnTypeCommand = Format(SqlConstants.CheckHeadersColumnType, this.qualifiedTableName);
#pragma warning restore 618
        }

        public virtual async Task<int> TryPeek(DbConnection connection, DbTransaction transaction, CancellationToken token, int timeoutInSeconds = 30)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandTimeout = timeoutInSeconds;
                command.CommandText = peekCommand;
                command.Connection = connection;
                command.Transaction = transaction;
                
                var numberOfMessages = (int) await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                return numberOfMessages;
            }
        }

        public void FormatPeekCommand(int maxRecordsToPeek)
        {
#pragma warning disable 618
            peekCommand = Format(SqlConstants.PeekText, qualifiedTableName, maxRecordsToPeek);
#pragma warning restore 618
        }

        public virtual async Task<MessageReadResult> TryReceive(DbConnection connection, DbTransaction transaction)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = receiveCommand;
                command.Connection = connection;
                command.Transaction = transaction;
                
                return await ReadMessage(command).ConfigureAwait(false);
            }
        }

        public Task DeadLetter(MessageRow poisonMessage, DbConnection connection, DbTransaction transaction)
        {
            return SendRawMessage(poisonMessage, connection, transaction);
        }

        public Task Send(OutgoingMessage message, TimeSpan timeToBeReceived, DbConnection connection, DbTransaction transaction)
        {
            var messageRow = MessageRow.From(message.Headers, message.Body, timeToBeReceived);

            return SendRawMessage(messageRow, connection, transaction);
        }

        static async Task<MessageReadResult> ReadMessage(DbCommand command)
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

        async Task SendRawMessage(MessageRow message, DbConnection connection, DbTransaction transaction)
        {
            try
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sendCommand;
                    command.Connection = connection;
                    command.Transaction = transaction;
                    
                    message.PrepareSendCommand(command);

                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
            // What to do here?
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

        void ThrowQueueNotFoundException(Exception ex)
        {
            throw new QueueNotFoundException(Name, $"Failed to send message to {qualifiedTableName}", ex);
        }

        void ThrowFailedToSendException(Exception ex)
        {
            throw new Exception($"Failed to send message to {qualifiedTableName}", ex);
        }

        public async Task<int> Purge(DbConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = purgeCommand;
                command.Connection = connection;

                return await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task<int> PurgeBatchOfExpiredMessages(DbConnection connection, int purgeBatchSize)
        {
            using (var command = connection.CreateCommand())
            {
                command.Connection = connection;
                command.CommandText = purgeExpiredCommand;

                var parameter = command.CreateParameter();
                parameter.ParameterName = "@BatchSize";
                parameter.Value = purgeBatchSize;
                command.Parameters.Add(parameter);
                    
                return await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task<bool> CheckExpiresIndexPresence(DbConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = checkIndexCommand;
                command.Connection = connection;

                var rowsCount = (int) await command.ExecuteScalarAsync().ConfigureAwait(false);
                return rowsCount > 0;
            }
        }

        public async Task<string> CheckHeadersColumnType(DbConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = checkHeadersColumnTypeCommand;
                command.Connection = connection;
                
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
        string checkIndexCommand;
        string checkHeadersColumnTypeCommand;
    }
}