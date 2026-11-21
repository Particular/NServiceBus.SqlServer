namespace NServiceBus.Transport.Sql.Shared
{
    using System.Threading;
    using System.Threading.Tasks;

    class QueuePurger(DbConnectionFactory connectionFactory) : IPurgeQueues
    {
        public virtual async Task<int> Purge(TableBasedQueue queue, CancellationToken cancellationToken = default)
        {
            var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false);
            await using var connectionDisposer = connection.ConfigureAwait(false);
            return await queue.Purge(connection, cancellationToken).ConfigureAwait(false);
        }
    }
}