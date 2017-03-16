namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Concurrent;

    class TableBasedQueueCollection
    {
        public TableBasedQueue GetQueue(QueueAddress address)
        {
            return cache.GetOrAdd(address, a => new TableBasedQueue(a));
        } 

        ConcurrentDictionary<QueueAddress, TableBasedQueue> cache = new ConcurrentDictionary<QueueAddress, TableBasedQueue>();
    }
}