namespace NServiceBus.Transport.SqlServer
{
    using System.Data;
    using System.Data.Common;
    using NServiceBus.Transport.Sql.Shared;

    class MoveDueDelayedMessagesWithLockCommand(SqlServerConstants sqlConstants, CanonicalQueueAddress delayedQueue, string inputQueueTable) : IMoveDueDelayedMessagesCommand
    {
        // Commits on the server so a stalled client cannot keep the mover lock
        public bool OwnsTransaction => true;

        public void Populate(DbCommand command, int batchSize)
        {
            command.CommandText = commandText;
            command.AddParameter("BatchSize", DbType.Int32, batchSize);
            command.AddParameter("MoverLockResource", DbType.String, LockResource, MaxLockResourceLength);
            command.AddParameter("MoverLockCatalog", DbType.String, delayedQueue.Catalog, 128);
        }

        // sp_getapplock compares resource names case-sensitively
        public string LockResource { get; } = $"{delayedQueue.QualifiedTableName}_mover".ToUpperInvariant();

        // sp_getapplock truncates longer resource names to this length
        const int MaxLockResourceLength = 255;

        // The lock runs in the delayed table's catalog because sp_getapplock scopes it to the session's database.
        // The batch qualifies the call only from another database, so a session in that catalog makes a plain call.
        readonly string commandText = string.Format(sqlConstants.MoveDueDelayedMessageWithLockText, delayedQueue.QualifiedTableName, inputQueueTable, delayedQueue.QuotedCatalogName);
    }
}
