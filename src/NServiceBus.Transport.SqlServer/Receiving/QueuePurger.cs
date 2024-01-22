namespace NServiceBus.Transport.SqlServer
{
    using System.Threading;
    using System.Threading.Tasks;

    class QueuePurger : IPurgeQueues
    {
        public QueuePurger(DbConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public virtual async Task<int> Purge(TableBasedQueue queue, CancellationToken cancellationToken = default)
        {
            using (var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
            {
                return await queue.Purge(connection, cancellationToken).ConfigureAwait(false);
            }
        }

        DbConnectionFactory connectionFactory;
    }
}