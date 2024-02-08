namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Collections.Concurrent;

    //TODO: can be moved to the abstraction layer
    class TableBasedQueueCache
    {
        public TableBasedQueueCache(Func<string, string, bool, TableBasedQueue> queueFactory, QueueAddressTranslator addressTranslator, bool isStreamSupported)
        {
            this.queueFactory = queueFactory;
            this.addressTranslator = addressTranslator;
            this.isStreamSupported = isStreamSupported;
        }

        public TableBasedQueue Get(string destination)
        {
            var address = addressTranslator.Parse(destination);
            var key = Tuple.Create(address.QualifiedTableName, address.Address);
            var queue = cache.GetOrAdd(key, x => queueFactory(x.Item1, x.Item2, isStreamSupported));

            return queue;
        }

        Func<string, string, bool, TableBasedQueue> queueFactory;
        QueueAddressTranslator addressTranslator;
        ConcurrentDictionary<Tuple<string, string>, TableBasedQueue> cache = new ConcurrentDictionary<Tuple<string, string>, TableBasedQueue>();
        bool isStreamSupported;
    }
}