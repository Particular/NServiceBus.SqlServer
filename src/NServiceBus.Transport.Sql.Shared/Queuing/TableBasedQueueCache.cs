namespace NServiceBus.Transport.Sql.Shared
{
    using System;
    using System.Collections.Concurrent;

    class TableBasedQueueCache
    {
        public TableBasedQueueCache(Func<string, bool, TableBasedQueue> queueFactory, Func<string, string> addressTranslator, bool isStreamSupported)
        {
            this.queueFactory = queueFactory;
            this.addressTranslator = addressTranslator;
            this.isStreamSupported = isStreamSupported;
        }

        public TableBasedQueue Get(string destination)
        {
            //Get a fully-qualified form of the name so that regardless in which format we get from the core/user, we cache based on a standardized from
            //to avoid having duplicate cache entries for a single table
            var key = addressTranslator(destination);
            var queue = cache.GetOrAdd(key, x => queueFactory(x, isStreamSupported));

            return queue;
        }

        Func<string, bool, TableBasedQueue> queueFactory;
        Func<string, string> addressTranslator;
        ConcurrentDictionary<string, TableBasedQueue> cache = new ConcurrentDictionary<string, TableBasedQueue>();
        bool isStreamSupported;
    }
}