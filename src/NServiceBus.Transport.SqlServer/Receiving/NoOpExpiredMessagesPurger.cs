namespace NServiceBus.Transport.SqlServer
{
    using System.Threading;
    using System.Threading.Tasks;
    using Sql.Shared.Receiving;

    class NoOpExpiredMessagesPurger : IExpiredMessagesPurger
    {
        public Task Purge(TableBasedQueue queue, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}