namespace NServiceBus.Transports.SQLServer
{
    using System.Threading.Tasks;

    class QueuePurger : IPurgeQueues
    {
        public QueuePurger(SqlConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public virtual async Task<int> Purge(ITableBasedQueue queue)
        {
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                var purgedRowsCount = await queue.Purge(connection).ConfigureAwait(false);

                return purgedRowsCount;
            }
        }

        SqlConnectionFactory connectionFactory;
    }
}