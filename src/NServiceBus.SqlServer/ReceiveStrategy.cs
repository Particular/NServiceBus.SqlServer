namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Threading.Tasks;

    abstract class ReceiveStrategy
    {
        public abstract Task RecieveMessage(string messageId, TableBasedQueue inputQueue, TableBasedQueue errorQueue, Func<PushContext, Task> onMessage);
    }
}