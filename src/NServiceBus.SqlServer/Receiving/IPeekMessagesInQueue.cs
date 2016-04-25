namespace NServiceBus.Transports.SQLServer
{
    using System.Threading;
    using System.Threading.Tasks;

    interface IPeekMessagesInQueue
    {
        Task<int> Peek(ITableBasedQueue inputQueue, RepeatedFailuresOverTimeCircuitBreaker circuitBreaker, CancellationToken cancellationToken);
    }
}