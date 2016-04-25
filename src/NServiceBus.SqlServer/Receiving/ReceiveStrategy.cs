namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    interface ReceiveStrategy
    {
        Task ReceiveMessage(ITableBasedQueue inputQueue, ITableBasedQueue errorQueue, CancellationTokenSource receiveCancellationTokenSource, Func<PushContext, Task> onMessage);
    }
}