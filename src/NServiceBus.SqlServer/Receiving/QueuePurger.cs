namespace NServiceBus.Transports.SQLServer
{
    using System.Threading.Tasks;

    class QueuePurger : IPurgeQueues
    {
        public QueuePurger(SqlConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public virtual async Task<int> Purge(TableBasedQueue queue)
        {
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                return await queue.Purge(connection).ConfigureAwait(false);
            }
        }

        SqlConnectionFactory connectionFactory;
    }
}