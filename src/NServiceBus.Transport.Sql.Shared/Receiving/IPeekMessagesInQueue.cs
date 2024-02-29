namespace NServiceBus.Transport.Sql.Shared.Receiving
{
    using System.Threading;
    using System.Threading.Tasks;
    using Queuing;

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
    public interface IPeekMessagesInQueue
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
    {
        Task<int> Peek(TableBasedQueue inputQueue, RepeatedFailuresOverTimeCircuitBreaker circuitBreaker, CancellationToken cancellationToken = default);
    }
}