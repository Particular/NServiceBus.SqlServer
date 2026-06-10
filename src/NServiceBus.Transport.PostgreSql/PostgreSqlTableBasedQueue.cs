namespace NServiceBus.Transport.PostgreSql;

using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using NServiceBus.Transport.Sql.Shared;
using Unicast.Queuing;

using static System.String;

class PostgreSqlTableBasedQueue(PostgreSqlConstants sqlConstants, string qualifiedTableName, string queueName, bool isStreamSupported)
    : TableBasedQueue(sqlConstants, qualifiedTableName, queueName, isStreamSupported)
{
    readonly string sendCommand = Format(sqlConstants.SendText, qualifiedTableName);

    protected override async Task SendRawMessage(MessageRow message, DbConnection connection, DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var command = connection.CreateCommand();
            command.CommandType = CommandType.Text;
            command.CommandText = sendCommand;
            command.Transaction = transaction;

            message.PrepareSendCommand(command);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        // see: PostgreSQL: Documentation: 16: Appendix A. PostgreSQL Error Codes
        catch (NpgsqlException ex) when (ex.SqlState == "42P01")
        {
            throw new QueueNotFoundException(Name, $"Failed to send message to {qualifiedTableName}", ex);
        }
        catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
        {
            throw new Exception($"Failed to send message to {qualifiedTableName}", ex);
        }
    }
}