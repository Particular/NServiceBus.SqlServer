namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Threading.Tasks;
    
    interface ReceiveStrategy
    {
        Task ReceiveMessage(string messageId, TableBasedQueue inputQueue, TableBasedQueue errorQueue, Func<PushContext, Task> onMessage);
    }
}
