namespace NServiceBus.Transport.SqlServer;

using System.Data;
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using static System.String;
using Microsoft.Data.SqlClient;
using Unicast.Queuing;

class SqlTableBasedQueue : TableBasedQueue
{
    public SqlTableBasedQueue(SqlServerConstants sqlConstants, string qualifiedTableName, string queueName, bool isStreamSupported) :
        base(sqlConstants, qualifiedTableName, queueName, isStreamSupported) =>
        sqlServerConstants = sqlConstants;

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

            var commandText = Format(sqlServerConstants.CheckIfTableHasRecoverableText, qualifiedTableName);
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
                            cachedSendCommand = Format(sqlServerConstants.SendTextWithRecoverable, qualifiedTableName);
                            return cachedSendCommand;
                        }
                    }
                }

                cachedSendCommand = Format(sqlServerConstants.SendText, qualifiedTableName);
                return cachedSendCommand;
            }
        }
        finally
        {
            sendCommandLock.Release();
        }
    }

    //TODO: check if this could be shared with PostgreSQL implementation
    void ThrowQueueNotFoundException(SqlException ex)
    {
        throw new QueueNotFoundException(Name, $"Failed to send message to {qualifiedTableName}", ex);
    }

    void ThrowFailedToSendException(Exception ex)
    {
        throw new Exception($"Failed to send message to {qualifiedTableName}", ex);
    }

    string cachedSendCommand;

    readonly SemaphoreSlim sendCommandLock = new SemaphoreSlim(1, 1);
    readonly SqlServerConstants sqlServerConstants;
}