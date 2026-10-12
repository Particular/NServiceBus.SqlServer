namespace NServiceBus.Transport.PostgreSql
{
    using System;
    using System.Data;
    using System.Data.Common;
    using NServiceBus.Transport.Sql.Shared;

    class MoveDueDelayedMessagesWithLockCommand(PostgreSqlConstants sqlConstants, string delayedQueueTable, string inputQueueTable, TimeSpan lockRetryInterval) : IMoveDueDelayedMessagesCommand
    {
        public void Populate(DbCommand command, int batchSize)
        {
            command.CommandText = commandText;
            command.AddParameter("BatchSize", DbType.Int32, batchSize);
            command.AddParameter("LockRetryIntervalMs", DbType.Int32, lockRetryIntervalMs);
        }

        readonly string commandText = string.Format(sqlConstants.MoveDueDelayedMessageWithLockText, delayedQueueTable, inputQueueTable);
        readonly int lockRetryIntervalMs = (int)Math.Ceiling(lockRetryInterval.TotalMilliseconds);
    }
}
