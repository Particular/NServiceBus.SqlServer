namespace NServiceBus.Transport.Sql.Shared.Receiving
{
    using System.Threading;
    using System.Threading.Tasks;
    using Queuing;

    public interface IExpiredMessagesPurger
    {
        Task Purge(TableBasedQueue queue, CancellationToken cancellationToken = default);
    }
}