namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Data;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using Microsoft.Data.SqlClient;
    using Unicast.Queuing;
    using static System.String;

    //TODO: move to the abstraction layer
    abstract class TableBasedQueue
    {
        public string Name { get; }

        public TableBasedQueue(ISqlConstants sqlConstants, string qualifiedTableName, string queueName, bool isStreamSupported)
        {
            this.sqlConstants = sqlConstants;
            this.qualifiedTableName = qualifiedTableName;
            Name = queueName;
            receiveCommand = Format(sqlConstants.ReceiveText, this.qualifiedTableName);
            purgeCommand = Format(sqlConstants.PurgeText, this.qualifiedTableName);
            purgeExpiredCommand = Format(sqlConstants.PurgeBatchOfExpiredMessagesText, this.qualifiedTableName);
            checkExpiresIndexCommand = Format(sqlConstants.CheckIfExpiresIndexIsPresent, this.qualifiedTableName);
            checkNonClusteredRowVersionIndexCommand = Format(sqlConstants.CheckIfNonClusteredRowVersionIndexIsPresent, this.qualifiedTableName);
            checkHeadersColumnTypeCommand = Format(sqlConstants.CheckHeadersColumnType, this.qualifiedTableName);
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
            peekCommand = Format(sqlConstants.PeekText, qualifiedTableName, maxRecordsToPeek);
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

        ISqlConstants sqlConstants;
        protected string qualifiedTableName;
        string peekCommand;
        string receiveCommand;
        string purgeCommand;
        string purgeExpiredCommand;
        string checkExpiresIndexCommand;
        string checkNonClusteredRowVersionIndexCommand;
        string checkHeadersColumnTypeCommand;
        bool isStreamSupported;

        static readonly ILog log = LogManager.GetLogger<TableBasedQueue>();
    }
}