namespace NServiceBus.Transport.PostgreSql;

using System.Data;
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using SqlServer;
using static System.String;
using NServiceBus.Unicast.Queuing;

class PostgreSqlTableBasedQueue : TableBasedQueue
{
    readonly PostgreSqlConstants postgreSqlConstants;

    public PostgreSqlTableBasedQueue(PostgreSqlConstants sqlConstants, string qualifiedTableName, string queueName, bool isStreamSupported) :
        base(sqlConstants, qualifiedTableName, queueName, isStreamSupported) =>
        postgreSqlConstants = sqlConstants;

    protected override async Task SendRawMessage(MessageRow message, DbConnection connection, DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sendCommand = Format(postgreSqlConstants.SendText, qualifiedTableName);

            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = sendCommand;
                command.Transaction = transaction;

                message.PrepareSendCommand(command);

                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        //TODO: figure out the error codes
        // see: PostgreSQL: Documentation: 16: Appendix A. PostgreSQL Error Codes
        catch (NpgsqlException ex) when (ex.ErrorCode == 208)
        {
            throw new QueueNotFoundException(Name, $"Failed to send message to {qualifiedTableName}", ex);
        }
        catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
        {
            throw new Exception($"Failed to send message to {qualifiedTableName}", ex);
        }
    }
}