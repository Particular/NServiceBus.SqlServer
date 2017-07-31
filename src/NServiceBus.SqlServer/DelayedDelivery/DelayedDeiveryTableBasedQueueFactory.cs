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

                return (conn, trans) => delayedMessageTable.Store(operation.Message, behavior.DueTime, behavior.Destination, conn, trans);
            }
            return immediateDeliveryQueueOperationsReader.Get(operation);
        }

        static DispatchBehavior GetDueTime(UnicastTransportOperation operation)
        {
            if (TryGetConstraint(operation, out DoNotDeliverBefore doNotDeliverBefore))
            {
                return DispatchBehavior.Deferred(doNotDeliverBefore.At, operation.Destination);
            }
            if (TryGetConstraint(operation, out DelayDeliveryWith delayDeliveryWith))
            {
                return DispatchBehavior.Deferred(DateTime.UtcNow + delayDeliveryWith.Delay, operation.Destination);
            }
            var headers = operation.Message.Headers;
            if (headers.TryGetValue(TimeoutManagerHeaders.Expire, out var expireString))
            {
                var expirationTime = DateTimeExtensions.ToUtcDateTime(expireString);
                var destination = headers[TimeoutManagerHeaders.RouteExpiredTimeoutTo];

                headers.Remove(TimeoutManagerHeaders.RouteExpiredTimeoutTo);
                headers.Remove(TimeoutManagerHeaders.Expire);

                return DispatchBehavior.Deferred(expirationTime, destination);
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
            public DateTime DueTime;
            public string Destination;

            public static DispatchBehavior Immediately()
            {
                return new DispatchBehavior();
            }

            public static DispatchBehavior Deferred(DateTime dueTime, string destination)
            {
                return new DispatchBehavior
                {
                    DueTime = dueTime,
                    Defer = true,
                    Destination = destination
                };
            }
        }

        static class TimeoutManagerHeaders
        {
            public const string Expire = "NServiceBus.Timeout.Expire";
            public const string RouteExpiredTimeoutTo = "NServiceBus.Timeout.RouteExpiredTimeoutTo";
        }
    }
}