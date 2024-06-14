namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Collections.Concurrent;

    class TableBasedQueueCache
    {
        public TableBasedQueueCache(QueueAddressTranslator addressTranslator, bool isStreamSupported)
        {
            this.addressTranslator = addressTranslator;
            this.isStreamSupported = isStreamSupported;
        }

        public TableBasedQueue Get(string destination)
        {
            var address = addressTranslator.Parse(destination);
            var key = Tuple.Create(address.QualifiedTableName, address.Address);
            var queue = cache.GetOrAdd(key, x => new TableBasedQueue(x.Item1, x.Item2, isStreamSupported));

            return queue;
        }

        QueueAddressTranslator addressTranslator;
        ConcurrentDictionary<Tuple<string, string>, TableBasedQueue> cache = new ConcurrentDictionary<Tuple<string, string>, TableBasedQueue>();
        bool isStreamSupported;
    }
}