namespace NServiceBus.Transport.SqlServer;

using System.Data;
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using static System.String;
using Microsoft.Data.SqlClient;
using Unicast.Queuing;
using Sql.Shared;
using Sql.Shared.Queuing;

class SqlTableBasedQueue : TableBasedQueue
{
    public SqlTableBasedQueue(SqlServerConstants sqlConstants, string qualifiedTableName, string queueName, bool isStreamSupported) :
        base(sqlConstants, qualifiedTableName, queueName, isStreamSupported)
    {
        sqlServerConstants = sqlConstants;

        purgeExpiredCommand = Format(sqlConstants.PurgeBatchOfExpiredMessagesText, this.qualifiedTableName);
        checkExpiresIndexCommand = Format(sqlConstants.CheckIfExpiresIndexIsPresent, this.qualifiedTableName);
        checkNonClusteredRowVersionIndexCommand = Format(sqlConstants.CheckIfNonClusteredRowVersionIndexIsPresent, this.qualifiedTableName);
        checkHeadersColumnTypeCommand = Format(sqlConstants.CheckHeadersColumnType, this.qualifiedTableName);
        checkRecoverableColumnColumnCommand = Format(sqlConstants.CheckIfTableHasRecoverableText, this.qualifiedTableName);
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

    protected override async Task SendRawMessage(MessageRow message, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
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

                _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
            throw new QueueNotFoundException(Name, $"Failed to send message to {qualifiedTableName}", ex);
        }
        catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
        {
            throw new Exception($"Failed to send message to {qualifiedTableName}", ex);
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

            using (var command = connection.CreateCommand())
            {
                command.CommandText = checkRecoverableColumnColumnCommand;
                command.CommandType = CommandType.Text;
                command.Transaction = transaction;

                var rowsCount = await command.ExecuteScalarAsync<int>(nameof(checkRecoverableColumnColumnCommand), cancellationToken).ConfigureAwait(false);
                if (rowsCount > 0)
                {
                    cachedSendCommand = Format(sqlServerConstants.SendTextWithRecoverable, qualifiedTableName);
                    return cachedSendCommand;
                }
                else
                {

                    cachedSendCommand = Format(sqlServerConstants.SendTextWithoutRecoverable, qualifiedTableName);
                    return cachedSendCommand;
                }
            }
        }
        finally
        {
            sendCommandLock.Release();
        }
    }

    string cachedSendCommand;
    readonly string purgeExpiredCommand;
    readonly string checkExpiresIndexCommand;
    readonly string checkNonClusteredRowVersionIndexCommand;
    readonly string checkHeadersColumnTypeCommand;
    readonly string checkRecoverableColumnColumnCommand;
    readonly SemaphoreSlim sendCommandLock = new SemaphoreSlim(1, 1);
    readonly SqlServerConstants sqlServerConstants;
}