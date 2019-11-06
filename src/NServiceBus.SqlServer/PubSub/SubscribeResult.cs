namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    class SubscribeResult
    {
        List<string> invokedNatively = new List<string>();
        List<string> invokedMessageDriven = new List<string>();

        public IEnumerable<string> InvokedNativelyOnly => invokedNatively.Except(invokedMessageDriven);

        public void InvokedMessageDriven(string eventType)
        {
            invokedMessageDriven.Add(eventType);
        }

        public void InvokedNatively(Type eventType)
        {
            invokedNatively.Add(eventType.AssemblyQualifiedName);
        }
    }
}