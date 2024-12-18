namespace NServiceBus.Transport.Sql.Shared
{
    using System.Threading;
    using System.Threading.Tasks;

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
    interface IPeekMessagesInQueue
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
    {
        Task<int> Peek(TableBasedQueue inputQueue, RepeatedFailuresOverTimeCircuitBreaker circuitBreaker, CancellationToken cancellationToken = default);
    }
}