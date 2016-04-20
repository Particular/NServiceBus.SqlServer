namespace NServiceBus.Transports.SQLServer.Legacy.MultiInstance
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Extensibility;

    class LegacyMessageDispatcher : IDispatchMessages
    {
        public LegacyMessageDispatcher(LegacyTableBasedQueueDispatcher dispatcher, QueueAddressParser addressParser)
        {
            this.dispatcher = dispatcher;
            this.addressParser = addressParser;
        }

        public async Task Dispatch(TransportOperations operations, ContextBag context)
        {
            var isolatedOperations = operations.UnicastTransportOperations.Where(o => o.RequiredDispatchConsistency == DispatchConsistency.Isolated);
            var deduplicatedIsolatedOperations = DeduplicateBasedOnMessageIdAndQueueAddress(isolatedOperations).ToList();
            await dispatcher.DispatchAsIsolated(deduplicatedIsolatedOperations).ConfigureAwait(false);

            var nonIsolatedOperations = operations.UnicastTransportOperations.Where(o => o.RequiredDispatchConsistency == DispatchConsistency.Default);
            var deduplicatedNonIsolatedOperations = DeduplicateBasedOnMessageIdAndQueueAddress(nonIsolatedOperations).ToList();
            await dispatcher.DispatchAsNonIsolated(deduplicatedNonIsolatedOperations).ConfigureAwait(false);
        }

        IEnumerable<MessageWithAddress> DeduplicateBasedOnMessageIdAndQueueAddress(IEnumerable<UnicastTransportOperation> isolatedConsistencyOperations)
        {
            var deduplicated = isolatedConsistencyOperations
                .Select(o => new MessageWithAddress(o.Message, addressParser.Parse(o.Destination)))
                .Distinct(OperationByMessageIdAndQueueAddressComparer);
            return deduplicated;
        }

        QueueAddressParser addressParser;
        LegacyTableBasedQueueDispatcher dispatcher;
        static OperationByMessageIdAndQueueAddressComparer OperationByMessageIdAndQueueAddressComparer = new OperationByMessageIdAndQueueAddressComparer();
    }
}