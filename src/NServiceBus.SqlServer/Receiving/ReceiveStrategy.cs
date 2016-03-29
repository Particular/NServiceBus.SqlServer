namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    interface ReceiveStrategy
    {
        Task ReceiveMessage(TableBasedQueue inputQueue, TableBasedQueue errorQueue, CancellationTokenSource cancellationTokenSource, Func<PushContext, Task> onMessage);
    }
}
