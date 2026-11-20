namespace NServiceBus.Transport.SqlServer
{
    using System.Data;
    using System.Data.Common;
    using NServiceBus.Transport.Sql.Shared;

    class MoveDueDelayedMessagesWithLockCommand(SqlServerConstants sqlConstants, string delayedQueueTable, string inputQueueTable) : IMoveDueDelayedMessagesCommand
    {
        // Commits on the server so a stalled client cannot keep the mover lock
        public bool OwnsTransaction => true;

        public void Populate(DbCommand command, int batchSize)
        {
            command.CommandText = commandText;
            command.AddParameter("BatchSize", DbType.Int32, batchSize);
            command.AddParameter("MoverLockResource", DbType.String, LockResource, MaxLockResourceLength);
        }

        public string LockResource { get; } = $"{delayedQueueTable}_mover";

        // sp_getapplock truncates longer resource names to this length
        const int MaxLockResourceLength = 255;

        readonly string commandText = string.Format(sqlConstants.MoveDueDelayedMessageWithLockText, delayedQueueTable, inputQueueTable);
    }
}
