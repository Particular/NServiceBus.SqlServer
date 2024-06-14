namespace NServiceBus.Transport.SqlServer
{
    using System.Threading;
    using System.Threading.Tasks;

    interface IPeekMessagesInQueue
    {
        Task<int> Peek(TableBasedQueue inputQueue, RepeatedFailuresOverTimeCircuitBreaker circuitBreaker, CancellationToken cancellationToken = default);
    }
}