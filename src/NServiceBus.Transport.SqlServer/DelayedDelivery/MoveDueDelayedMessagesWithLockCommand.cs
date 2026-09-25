namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Data;
    using System.Data.Common;
    using NServiceBus.Transport.Sql.Shared;

    class MoveDueDelayedMessagesWithLockCommand(SqlServerConstants sqlConstants, string delayedQueueTable, string inputQueueTable, TimeSpan lockDelay) : IMoveDueDelayedMessagesCommand
    {
        public void Populate(DbCommand command, int batchSize)
        {
            command.CommandText = commandText;
            command.AddParameter("BatchSize", DbType.Int32, batchSize);
            command.AddParameter("LockDelayMs", DbType.Int32, lockDelayMs);
        }

        readonly string commandText = string.Format(sqlConstants.MoveDueDelayedMessageWithLockText, delayedQueueTable, inputQueueTable);
        readonly int lockDelayMs = (int)Math.Ceiling(lockDelay.TotalMilliseconds);
    }
}
