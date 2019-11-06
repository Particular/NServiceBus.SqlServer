namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    class NativelySubscribedEvents
    {
        List<Func<Type, bool>> nativelySubscribedEvents = new List<Func<Type, bool>>();

        public void Add(Func<Type, bool> filterCallback)
        {
            nativelySubscribedEvents.Add(filterCallback);
        }

        public bool IsNativelySubscribed(Type eventType)
        {
            return nativelySubscribedEvents.Any(x => x(eventType));
        }
    }
}