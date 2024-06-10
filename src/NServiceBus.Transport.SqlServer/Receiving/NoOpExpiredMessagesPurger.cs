namespace NServiceBus.Transport.SqlServer
{
    using System.Threading;
    using System.Threading.Tasks;

    class NoOpExpiredMessagesPurger : IExpiredMessagesPurger
    {
        public Task Purge(SqlTableBasedQueue queue, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}