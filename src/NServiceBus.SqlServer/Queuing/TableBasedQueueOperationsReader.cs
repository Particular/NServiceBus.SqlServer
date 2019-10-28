namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Concurrent;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Threading.Tasks;
    using DelayedDelivery;
    using DeliveryConstraints;
    using Performance.TimeToBeReceived;

    class TableBasedQueueOperationsReader : ITableBasedQueueOperationsReader
    {
        public TableBasedQueueOperationsReader(QueueAddressTranslator addressTranslator, DelayedMessageTable delayedMessageTable)
        {
            this.addressTranslator = addressTranslator;
            this.delayedMessageTable = delayedMessageTable;
        }

        public Func<SqlConnection, SqlTransaction, Task> Get(UnicastTransportOperation operation)
        {
            var behavior = GetDueTime(operation);
            TryGetConstraint(operation, out DiscardIfNotReceivedBefore discardIfNotReceivedBefore);
            if (behavior.Defer)
            {
                // align with TimeoutManager behavior
                if (discardIfNotReceivedBefore != null && discardIfNotReceivedBefore.MaxTime < TimeSpan.MaxValue)
                {
                    throw new Exception("Delayed delivery of messages with TimeToBeReceived set is not supported. Remove the TimeToBeReceived attribute to delay messages of this type.");
                }

                return (conn, trans) => delayedMessageTable.Store(operation.Message, behavior.DueAfter, behavior.Destination, conn, trans);
            }

            var address = addressTranslator.Parse(operation.Destination);
            var key = Tuple.Create(address.QualifiedTableName, address.Address);
            var queue = cache.GetOrAdd(key, x => new TableBasedQueue(x.Item1, x.Item2));

            return (conn, trans) => queue.Send(operation.Message, discardIfNotReceivedBefore?.MaxTime ?? TimeSpan.MaxValue, conn, trans);
        }

        static DispatchBehavior GetDueTime(UnicastTransportOperation operation)
        {
            if (TryGetConstraint(operation, out DoNotDeliverBefore doNotDeliverBefore))
            {
                return DispatchBehavior.Deferred(doNotDeliverBefore.At - DateTime.UtcNow, operation.Destination);
            }
            if (TryGetConstraint(operation, out DelayDeliveryWith delayDeliveryWith))
            {
                return DispatchBehavior.Deferred(delayDeliveryWith.Delay, operation.Destination);
            }
            return DispatchBehavior.Immediately();
        }

        static bool TryGetConstraint<T>(IOutgoingTransportOperation operation, out T constraint) where T : DeliveryConstraint
        {
            constraint = operation.DeliveryConstraints.OfType<T>().FirstOrDefault();
            return constraint != null;
        }

        struct DispatchBehavior
        {
            public bool Defer;
            public TimeSpan DueAfter;
            public string Destination;

            public static DispatchBehavior Immediately()
            {
                return new DispatchBehavior();
            }

            public static DispatchBehavior Deferred(TimeSpan dueAfter, string destination)
            {
                return new DispatchBehavior
                {
                    DueAfter = dueAfter < TimeSpan.Zero ? TimeSpan.Zero : dueAfter,
                    Defer = true,
                    Destination = destination
                };
            }
        }

        DelayedMessageTable delayedMessageTable;
        QueueAddressTranslator addressTranslator;
        ConcurrentDictionary<Tuple<string, string>, TableBasedQueue> cache = new ConcurrentDictionary<Tuple<string, string>, TableBasedQueue>();
    }
}