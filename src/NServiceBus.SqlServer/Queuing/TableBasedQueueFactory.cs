namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Concurrent;

    class TableBasedQueueFactory
    {
        public TableBasedQueue Get(string qualifiedTableName, string queueName)
        {
            var key = Tuple.Create(qualifiedTableName, queueName);
            return cache.GetOrAdd(key, x => new TableBasedQueue(x.Item1, x.Item2));
        } 

        ConcurrentDictionary<Tuple<string, string>, TableBasedQueue> cache = new ConcurrentDictionary<Tuple<string, string>, TableBasedQueue>();
    }
}