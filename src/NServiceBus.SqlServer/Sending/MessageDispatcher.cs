namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Extensibility;
    using Transports;

    class MessageDispatcher : IDispatchMessages
    {
        public MessageDispatcher(IQueueDispatcher dispatcher, QueueAddressParser addressParser)
        {
            this.dispatcher = dispatcher;
            this.addressParser = addressParser;
        }

        // We need to check if we can support cancellation in here as well?
        public async Task Dispatch(TransportOperations operations, ContextBag context)
        {
            await Dispatch(operations, dispatcher.DispatchAsIsolated, DispatchConsistency.Isolated).ConfigureAwait(false);
            await Dispatch(operations, ops => dispatcher.DispatchAsNonIsolated(ops, context), DispatchConsistency.Default).ConfigureAwait(false);
        }

        Task Dispatch(TransportOperations operations, Func<List<MessageWithAddress>, Task> dispatchMethod, DispatchConsistency dispatchConsistency)
        {
            var isolatedOperations = operations.UnicastTransportOperations.Where(o => o.RequiredDispatchConsistency == dispatchConsistency);
            var deduplicatedIsolatedOperations = DeduplicateBasedOnMessageIdAndQueueAddress(isolatedOperations).ToList();
            return dispatchMethod(deduplicatedIsolatedOperations);
        }

        IEnumerable<MessageWithAddress> DeduplicateBasedOnMessageIdAndQueueAddress(IEnumerable<UnicastTransportOperation> isolatedConsistencyOperations)
        {
            return isolatedConsistencyOperations
                .Select(o => new MessageWithAddress(o.Message, addressParser.Parse(o.Destination)))
                .Distinct(OperationByMessageIdAndQueueAddressComparer);
        }

        IQueueDispatcher dispatcher;
        QueueAddressParser addressParser;
        static OperationByMessageIdAndQueueAddressComparer OperationByMessageIdAndQueueAddressComparer = new OperationByMessageIdAndQueueAddressComparer();
    }
}