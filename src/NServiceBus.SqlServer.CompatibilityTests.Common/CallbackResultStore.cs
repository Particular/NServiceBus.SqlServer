namespace CompatibilityTests.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class CallbackResultStore
    {
        Dictionary<Type, List<object>> results = new Dictionary<Type, List<object>>();

        public void Add<T>(T result)
        {
            lock(results)
            {
                if (results.ContainsKey(typeof(T)) == false)
                {
                    results.Add(typeof(T), new List<object>());
                }

                results[typeof(T)].Add(result);
            }
        }

        public T[] Get<T>()
        {
            lock (results)
            {
                if (results.ContainsKey(typeof(T)) == false)
                {
                    return new T[0];
                }

                return results[typeof(T)].Cast<T>().ToArray();
            }
        }
    }
}