namespace NServiceBus.Transports.SQLServer
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Extensibility;

    class MessageDispatcher : IDispatchMessages
    {
        public MessageDispatcher(TableBasedQueueDispatcher dispatcher, QueueAddressParser addressParser)
        {
            this.dispatcher = dispatcher;
            this.addressParser = addressParser;
        }

        // We need to check if we can support cancellation in here as well?
        public async Task Dispatch(TransportOperations operations, ContextBag context)
        {
            var isolatedOperations = operations.UnicastTransportOperations.Where(o => o.RequiredDispatchConsistency == DispatchConsistency.Isolated);
            var deduplicatedIsolatedOperations = DeduplicateBasedOnMessageIdAndQueueAddress(isolatedOperations).ToList();
            await dispatcher.DispatchAsIsolated(deduplicatedIsolatedOperations).ConfigureAwait(false);

            var nonIsolatedOperations = operations.UnicastTransportOperations.Where(o => o.RequiredDispatchConsistency == DispatchConsistency.Default);
            var deduplicatedNonIsolatedOperations = DeduplicateBasedOnMessageIdAndQueueAddress(nonIsolatedOperations).ToList();
            await DispatchAsNonIsolated(context, deduplicatedNonIsolatedOperations).ConfigureAwait(false);
        }

        IEnumerable<MessageWithAddress> DeduplicateBasedOnMessageIdAndQueueAddress(IEnumerable<UnicastTransportOperation> isolatedConsistencyOperations)
        {
            var deduplicated = isolatedConsistencyOperations
                .Select(o => new MessageWithAddress(o.Message, addressParser.Parse(o.Destination)))
                .Distinct(OperationByMessageIdAndQueueAddressComparer);
            return deduplicated;
        }

        async Task DispatchAsNonIsolated(ContextBag context, List<MessageWithAddress> defaultConsistencyOperations)
        {
            TransportTransaction transportTransaction;
            var transportTransactionExists = context.TryGet(out transportTransaction);

            if (transportTransactionExists == false || InReceiveOnlyTransportTransactionMode(transportTransaction))
            {
                await dispatcher.DispatchOperationsWithNewConnectionAndTransaction(defaultConsistencyOperations).ConfigureAwait(false);
                return;
            }

            await dispatcher.DispatchUsingReceiveTransaction(transportTransaction, defaultConsistencyOperations).ConfigureAwait(false);
        }

        static bool InReceiveOnlyTransportTransactionMode(TransportTransaction transportTransaction)
        {
            bool inReceiveMode;
            return transportTransaction.TryGet(ReceiveWithReceiveOnlyTransaction.ReceiveOnlyTransactionMode, out inReceiveMode);
        }

        readonly TableBasedQueueDispatcher dispatcher;
        QueueAddressParser addressParser;

        static OperationByMessageIdAndQueueAddressComparer OperationByMessageIdAndQueueAddressComparer = new OperationByMessageIdAndQueueAddressComparer();
    }

    class MessageWithAddress
    {
        public QueueAddress Address { get; }
        public OutgoingMessage Message { get; }

        public MessageWithAddress(OutgoingMessage message, QueueAddress address)
        {
            Address = address;
            Message = message;
        }
    }

    class OperationByMessageIdAndQueueAddressComparer : IEqualityComparer<MessageWithAddress>
    {
        public bool Equals(MessageWithAddress x, MessageWithAddress y)
        {
            return x.Message.MessageId.Equals(y.Message.MessageId)
                   && x.Address.TableName.Equals(y.Address.TableName)
                   && x.Address.SchemaName.Equals(y.Address.SchemaName);
        }

        public int GetHashCode(MessageWithAddress obj)
        {
            unchecked
            {
                var hashCode = obj.Message.MessageId.GetHashCode();
                hashCode = (hashCode * 397) ^ obj.Address.TableName.GetHashCode();
                hashCode = (hashCode * 397) ^ obj.Address.SchemaName.GetHashCode();
                return hashCode;
            }
        }
    }

}