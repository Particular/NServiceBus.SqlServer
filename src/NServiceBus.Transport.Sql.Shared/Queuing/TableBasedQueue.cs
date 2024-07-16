namespace NServiceBus.Transport.Sql.Shared.Queuing
{
    using System;
    using System.Data;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using static System.String;

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
    abstract class TableBasedQueue
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
    {
        public string Name { get; }

        public TableBasedQueue(ISqlConstants sqlConstants, string qualifiedTableName, string queueName, bool isStreamSupported)
        {
            this.sqlConstants = sqlConstants;
            this.qualifiedTableName = qualifiedTableName;
            Name = queueName;
            receiveCommand = Format(sqlConstants.ReceiveText, this.qualifiedTableName);
            purgeCommand = Format(sqlConstants.PurgeText, this.qualifiedTableName);
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

        public void FormatPeekCommand()
        {
            peekCommand = Format(sqlConstants.PeekText, qualifiedTableName);
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

        protected abstract Task SendRawMessage(MessageRow message, DbConnection connection, DbTransaction transaction,
            CancellationToken cancellationToken = default);


        public async Task<int> Purge(DbConnection connection, CancellationToken cancellationToken = default)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = purgeCommand;
                command.CommandType = CommandType.Text;

                return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public override string ToString()
        {
            return qualifiedTableName;
        }

        ISqlConstants sqlConstants;
        protected string qualifiedTableName;
        string peekCommand;
        string receiveCommand;
        string purgeCommand;
        bool isStreamSupported;

        static readonly ILog log = LogManager.GetLogger<TableBasedQueue>();
    }
}