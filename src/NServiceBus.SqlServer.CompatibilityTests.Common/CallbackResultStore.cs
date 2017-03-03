namespace NServiceBus.SqlServer.CompatibilityTests.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class CallbackResultStore
    {
        Dictionary<Type, object> results = new Dictionary<Type, object>();

        public void Add<T>(T result)
        {
            results.Add(typeof(T), result);
        }

        public T[] Get<T>()
        {
            return results.Where(kv => kv.Key == typeof(T)).Select(kv => (T)kv.Value).ToArray();
        }
    }
}