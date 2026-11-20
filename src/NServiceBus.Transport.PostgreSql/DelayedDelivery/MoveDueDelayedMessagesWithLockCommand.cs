namespace NServiceBus.Transport.PostgreSql
{
    using System.Data;
    using System.Data.Common;
    using System.IO.Hashing;
    using System.Text;
    using NServiceBus.Transport.Sql.Shared;

    class MoveDueDelayedMessagesWithLockCommand(PostgreSqlConstants sqlConstants, string delayedQueueTable, string inputQueueTable) : IMoveDueDelayedMessagesCommand
    {
        // Commits on the server so a stalled client cannot keep the mover lock
        public bool OwnsTransaction => true;

        public void Populate(DbCommand command, int batchSize)
        {
            command.CommandText = commandText;
            command.AddParameter("BatchSize", DbType.Int32, batchSize);
            command.AddParameter("MoverLockKey", DbType.Int64, LockKey);
        }

        // Every instance must derive the same key, so the hash cannot change between versions or processes
        public long LockKey { get; } = unchecked((long)XxHash64.HashToUInt64(Encoding.UTF8.GetBytes($"{delayedQueueTable}_mover")));

        readonly string commandText = string.Format(sqlConstants.MoveDueDelayedMessageWithLockText, delayedQueueTable, inputQueueTable);
    }
}
