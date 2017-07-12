namespace NServiceBus.Transport.SQLServer
{
    using System.Threading.Tasks;

    class LegacyQueuePurger : IPurgeQueues
    {
        public LegacyQueuePurger(LegacySqlConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public virtual async Task<int> Purge(TableBasedQueue queue)
        {
            using (var connection = await connectionFactory.OpenNewConnection(queue.Name).ConfigureAwait(false))
            {
                return await queue.Purge(connection).ConfigureAwait(false);
            }
        }

        LegacySqlConnectionFactory connectionFactory;
    }
}