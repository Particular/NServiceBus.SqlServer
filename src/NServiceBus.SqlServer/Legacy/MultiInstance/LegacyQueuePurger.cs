namespace NServiceBus.Transports.SQLServer.Legacy.MultiInstance
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
            using (var connection = await connectionFactory.OpenNewConnection(queue.TransportAddress).ConfigureAwait(false))
            {
                var purgedRowsCount = await queue.Purge(connection).ConfigureAwait(false);

                return purgedRowsCount;
            }
        }

        LegacySqlConnectionFactory connectionFactory;
    }
}