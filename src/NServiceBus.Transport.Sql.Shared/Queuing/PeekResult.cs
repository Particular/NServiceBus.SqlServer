namespace NServiceBus.Transport.Sql.Shared
{
    readonly struct PeekResult(int messageCount, long lowestRowVersion)
    {
        public static readonly PeekResult Empty = new(0, 0);

        /// <summary>
        /// An estimate of the number of messages in the queue.
        /// </summary>
        public int MessageCount { get; } = messageCount;

        /// <summary>
        /// The lowest row version not locked by an in-flight receive.
        /// </summary>
        public long LowestRowVersion { get; } = lowestRowVersion;
    }
}
