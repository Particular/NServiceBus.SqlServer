namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Threading.Tasks;
    using DelayedDelivery;
    using DeliveryConstraints;
    using Performance.TimeToBeReceived;

    class DelayedDeliveryTableBasedQueueOperationsReader : ITableBasedQueueOperationsReader
    {
        public DelayedDeliveryTableBasedQueueOperationsReader(DelayedMessageTable delayedMessageTable, ITableBasedQueueOperationsReader immediateDeliveryQueueOperationsReader)
        {
            this.delayedMessageTable = delayedMessageTable;
            this.immediateDeliveryQueueOperationsReader = immediateDeliveryQueueOperationsReader;
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
            return immediateDeliveryQueueOperationsReader.Get(operation);
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

        DelayedMessageTable delayedMessageTable;
        ITableBasedQueueOperationsReader immediateDeliveryQueueOperationsReader;

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
                    DueAfter = dueAfter,
                    Defer = true,
                    Destination = destination
                };
            }
        }
    }
}