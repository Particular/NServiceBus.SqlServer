namespace NServiceBus.Transport.SqlServer
{
    using System.Threading;
    using System.Threading.Tasks;

    interface IPurgeQueues
    {
        Task<int> Purge(TableBasedQueue queue, CancellationToken cancellationToken = default);
    }
}