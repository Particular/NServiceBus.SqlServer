namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Collections.Concurrent;

    class TableBasedQueueCache
    {
        public TableBasedQueueCache(ISqlConstants sqlConstants, QueueAddressTranslator addressTranslator, bool isStreamSupported)
        {
            this.sqlConstants = sqlConstants;
            this.addressTranslator = addressTranslator;
            this.isStreamSupported = isStreamSupported;
        }

        public TableBasedQueue Get(string destination)
        {
            var address = addressTranslator.Parse(destination);
            var key = Tuple.Create(address.QualifiedTableName, address.Address);
            var queue = cache.GetOrAdd(key, x => new TableBasedQueue(sqlConstants, x.Item1, x.Item2, isStreamSupported));

            return queue;
        }

        ISqlConstants sqlConstants;
        QueueAddressTranslator addressTranslator;
        ConcurrentDictionary<Tuple<string, string>, TableBasedQueue> cache = new ConcurrentDictionary<Tuple<string, string>, TableBasedQueue>();
        bool isStreamSupported;
    }
}