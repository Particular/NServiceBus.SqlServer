namespace CompatibilityTests.Common
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;

    public class MessageStore
    {
        ConcurrentBag<Tuple<Guid, Type>> messageIds = new ConcurrentBag<Tuple<Guid, Type>>();

        public void Add<T>(Guid id)
        {
            messageIds.Add(Tuple.Create(id, typeof(T)));
        }

        public Guid[] Get<T>()
        {
            return messageIds.Where(kv => kv.Item2 == typeof (T)).Select(kv => kv.Item1).ToArray();
        }

        public Guid[] GetAll()
        {
            return messageIds.Select(kv => kv.Item1).ToArray();
        }
    }
}