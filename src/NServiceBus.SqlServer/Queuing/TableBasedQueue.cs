#pragma warning disable 618
namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using Transport;
    using Unicast.Queuing;
    using static System.String;

    class TableBasedQueue
    {
        public TableBasedQueue(QueueAddress address)
        {
            using (var sanitizer = new SqlCommandBuilder())
            {
                tableName = sanitizer.QuoteIdentifier(address.TableName);
                schemaName = sanitizer.QuoteIdentifier(address.SchemaName);
            }

            TransportAddress = address.ToString();
        }

        public string TransportAddress { get; }

        public virtual async Task<int> TryPeek(SqlConnection connection, CancellationToken token, int timeoutInSeconds = 30)
        {
            var commandText = Format(SqlConstants.PeekText, schemaName, tableName);

            using (var command = new SqlCommand(commandText, connection)
            {
                CommandTimeout = timeoutInSeconds
            })
            {
                return (int) await command.ExecuteScalarAsync(token).ConfigureAwait(false);
            }
        }

        public virtual async Task<MessageReadResult> TryReceive(SqlConnection connection, SqlTransaction transaction)
        {
            var commandText = Format(SqlConstants.ReceiveText, schemaName, tableName);

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

                return await MessageRow.Read(dataReader).ConfigureAwait(false);
            }
        }

        async Task SendRawMessage(MessageRow message, SqlConnection connection, SqlTransaction transaction)
        {
            var commandText = Format(SqlConstants.SendText, schemaName, tableName);

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
            var queue = tableName == null
                ? null
                : ToString();

            var msg = tableName == null
                ? "Failed to send message. Target address is null."
                : $"Failed to send message to {queue}";

            throw new QueueNotFoundException(queue, msg, ex);
        }

        void ThrowFailedToSendException(Exception ex)
        {
            if (tableName == null)
            {
                throw new Exception("Failed to send message.", ex);
            }
            throw new Exception($"Failed to send message to {ToString()}", ex);
        }

        public async Task<int> Purge(SqlConnection connection)
        {
            var commandText = Format(SqlConstants.PurgeText, schemaName, tableName);

            using (var command = new SqlCommand(commandText, connection))
            {
                return await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task<int> PurgeBatchOfExpiredMessages(SqlConnection connection, int purgeBatchSize)
        {
            var commandText = Format(SqlConstants.PurgeBatchOfExpiredMessagesText, purgeBatchSize, schemaName, tableName);

            using (var command = new SqlCommand(commandText, connection))
            {
                return await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task LogWarningWhenIndexIsMissing(SqlConnection connection)
        {
            var commandText = Format(SqlConstants.CheckIfExpiresIndexIsPresent, schemaName, tableName);

            using (var command = new SqlCommand(commandText, connection))
            {
                var rowsCount = (int) await command.ExecuteScalarAsync().ConfigureAwait(false);

                if (rowsCount == 0)
                {
                    Logger.Warn($@"Table {schemaName}.{tableName} does not contain index 'Index_Expires'.{Environment.NewLine}Adding this index will speed up the process of purging expired messages from the queue. Please consult the documentation for further information.");
                }
            }
        }

        public override string ToString()
        {
            return $"{schemaName}.{tableName}";
        }

        string tableName;
        string schemaName;

        static ILog Logger = LogManager.GetLogger(typeof(TableBasedQueue));
    }
}