namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Concurrent;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Threading.Tasks;
    using DeliveryConstraints;
    using Performance.TimeToBeReceived;

    class TableBasedQueueOperationsReader : ITableBasedQueueOperationsReader
    {
        public TableBasedQueueOperationsReader(QueueAddressTranslator addressTranslator)
        {
            this.addressTranslator = addressTranslator;
        }

        public Func<SqlConnection, SqlTransaction, Task> Get(UnicastTransportOperation operation)
        {
            DiscardIfNotReceivedBefore discardIfNotReceivedBefore;
            TryGetConstraint(operation, out discardIfNotReceivedBefore);

            var address = addressTranslator.Parse(operation.Destination);
            var key = Tuple.Create(address.QualifiedTableName, address.Address);
            var queue = cache.GetOrAdd(key, x => new TableBasedQueue(x.Item1, x.Item2));

            return (conn, trans) => queue.Send(operation.Message, discardIfNotReceivedBefore?.MaxTime ?? TimeSpan.MaxValue, conn, trans);
        }

        static void TryGetConstraint<T>(IOutgoingTransportOperation operation, out T constraint) where T : DeliveryConstraint
        {
            constraint = operation.DeliveryConstraints.OfType<T>().FirstOrDefault();
        }

        QueueAddressTranslator addressTranslator;
        ConcurrentDictionary<Tuple<string, string>, TableBasedQueue> cache = new ConcurrentDictionary<Tuple<string, string>, TableBasedQueue>();
    }
}