namespace NServiceBus.Transport.PostgreSql
{
    using System.Data;
    using System.Data.Common;
    using NServiceBus.Transport.Sql.Shared;

    class MoveDueDelayedMessagesWithLockCommand(PostgreSqlConstants sqlConstants, string delayedQueueTable, string inputQueueTable) : IMoveDueDelayedMessagesCommand
    {
        public void Populate(DbCommand command, int batchSize)
        {
            command.CommandText = commandText;
            command.AddParameter("BatchSize", DbType.Int32, batchSize);
        }

        readonly string commandText = string.Format(sqlConstants.MoveDueDelayedMessageWithLockText, delayedQueueTable, inputQueueTable);
    }
}
