namespace NServiceBus.Transport.Sql.Shared.Receiving
{
    using System.Threading;
    using System.Threading.Tasks;
    using Queuing;

    public class NoOpExpiredMessagesPurger : IExpiredMessagesPurger
    {
        public Task Purge(TableBasedQueue queue, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}