namespace NServiceBus.Transport.Sql.Shared
{
    using System.Data;
    using System.Data.Common;

    class MoveDueDelayedMessagesCommand(ISqlConstants sqlConstants, string delayedQueueTable, string inputQueueTable) : IMoveDueDelayedMessagesCommand
    {
        public void Populate(DbCommand command, int batchSize)
        {
            command.CommandText = commandText;
            command.AddParameter("BatchSize", DbType.Int32, batchSize);
        }

        readonly string commandText = string.Format(sqlConstants.MoveDueDelayedMessageText, delayedQueueTable, inputQueueTable);
    }
}
