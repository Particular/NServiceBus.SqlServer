namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Concurrent;

    class TableBasedQueueFactory
    {
        public TableBasedQueue Get(QueueAddress address)
        {
            return cache.GetOrAdd(address, a => new TableBasedQueue(a));
        } 

        ConcurrentDictionary<QueueAddress, TableBasedQueue> cache = new ConcurrentDictionary<QueueAddress, TableBasedQueue>();
    }
}