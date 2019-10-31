namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Concurrent;

    class TableBasedQueueCache
    {
        public TableBasedQueueCache(QueueAddressTranslator addressTranslator)
        {
            this.addressTranslator = addressTranslator;
        }

        public TableBasedQueue Get(string destination)
        {
            var address = addressTranslator.Parse(destination);
            var key = Tuple.Create(address.QualifiedTableName, address.Address);
            var queue = cache.GetOrAdd(key, x => new TableBasedQueue(x.Item1, x.Item2));

            return queue;
        }

        QueueAddressTranslator addressTranslator;
        ConcurrentDictionary<Tuple<string, string>, TableBasedQueue> cache = new ConcurrentDictionary<Tuple<string, string>, TableBasedQueue>();
    }
}