namespace NServiceBus.Transport.SqlServer
{
    using System.Threading;
    using System.Threading.Tasks;

    interface IExpiredMessagesPurger
    {
        Task Purge(TableBasedQueue queue, CancellationToken cancellationToken = default);
    }
}