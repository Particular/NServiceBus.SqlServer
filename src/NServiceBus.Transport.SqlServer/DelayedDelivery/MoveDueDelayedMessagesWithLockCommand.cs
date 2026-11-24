namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Data;
    using System.Data.Common;
    using System.IO.Hashing;
    using System.Text;
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

        public string LockResource { get; } = CreateLockResource(delayedQueue);

        // sp_getapplock compares resource names case-sensitively and truncates them to 255 characters,
        // so a longer name keeps a prefix and ends in a hash of the whole name instead of losing its tail.
        static string CreateLockResource(CanonicalQueueAddress delayedQueue)
        {
            var resource = $"{delayedQueue.QualifiedTableName}_mover".ToUpperInvariant();
            if (resource.Length <= MaxLockResourceLength)
            {
                return resource;
            }

            // Hex digits never end in "_MOVER", so a hashed resource cannot equal one that fits
            var hash = Convert.ToHexString(XxHash128.Hash(Encoding.UTF8.GetBytes(resource)));
            return string.Concat(resource.AsSpan(0, MaxLockResourceLength - hash.Length), hash);
        }

        const int MaxLockResourceLength = 255;

        // The lock runs in the delayed table's catalog because sp_getapplock scopes it to the session's database.
        // The batch qualifies the call only from another database, so a session in that catalog makes a plain call.
        readonly string commandText = string.Format(sqlConstants.MoveDueDelayedMessageWithLockText, delayedQueue.QualifiedTableName, inputQueueTable, delayedQueue.QuotedCatalogName);
    }
}
