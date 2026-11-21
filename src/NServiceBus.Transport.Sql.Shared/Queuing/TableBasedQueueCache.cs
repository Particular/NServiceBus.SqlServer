namespace NServiceBus.Transport.Sql.Shared
{
    using System;
    using System.Collections.Concurrent;

    class TableBasedQueueCache(Func<string, bool, TableBasedQueue> queueFactory, Func<string, string> addressTranslator, bool isStreamSupported)
    {
        public TableBasedQueue Get(string destination)
        {
            //Get a fully-qualified form of the name so that regardless in which format we get from the core/user, we cache based on a standardized from
            //to avoid having duplicate cache entries for a single table
            var key = addressTranslator(destination);
            var queue = cache.GetOrAdd(key, static (x, state) => state.queueFactory(x, state.isStreamSupported), (queueFactory, isStreamSupported));

            return queue;
        }

        readonly ConcurrentDictionary<string, TableBasedQueue> cache = [];
    }
}