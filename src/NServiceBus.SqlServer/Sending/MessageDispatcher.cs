namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Extensibility;
    using Transport;

    class MessageDispatcher : IDispatchMessages
    {
        public MessageDispatcher(IQueueDispatcher dispatcher, QueueAddressParser addressParser)
        {
            this.dispatcher = dispatcher;
            this.addressParser = addressParser;
        }

        // We need to check if we can support cancellation in here as well?
        public async Task Dispatch(TransportOperations operations, TransportTransaction transportTransaction, ContextBag context)
        {
            await Dispatch(operations, dispatcher.DispatchAsIsolated, DispatchConsistency.Isolated).ConfigureAwait(false);
            await Dispatch(operations, ops => dispatcher.DispatchAsNonIsolated(ops, transportTransaction), DispatchConsistency.Default).ConfigureAwait(false);
        }

        Task Dispatch(TransportOperations operations, Func<HashSet<MessageWithAddress>, Task> dispatchMethod, DispatchConsistency dispatchConsistency)
        {
            var deduplicatedOperations = new HashSet<MessageWithAddress>(OperationByMessageIdAndQueueAddressComparer);
            foreach (var operation in operations.UnicastTransportOperations)
            {
                if (operation.RequiredDispatchConsistency == dispatchConsistency)
                {
                    deduplicatedOperations.Add(new MessageWithAddress(operation.Message, addressParser.Parse(operation.Destination)));
                }
            }
            return dispatchMethod(deduplicatedOperations);
        }

        IQueueDispatcher dispatcher;
        QueueAddressParser addressParser;
        static OperationByMessageIdAndQueueAddressComparer OperationByMessageIdAndQueueAddressComparer = new OperationByMessageIdAndQueueAddressComparer();
    }
}