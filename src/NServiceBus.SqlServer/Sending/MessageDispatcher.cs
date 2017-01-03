namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Extensibility;
    using Transport;

    class MessageDispatcher : IDispatchMessages
    {
        public MessageDispatcher(IQueueDispatcher dispatcher, QueueAddressParser addressParser, SchemaAndCatalogSettings locationSettings)
        {
            this.dispatcher = dispatcher;
            this.addressParser = addressParser;
            this.locationSettings = locationSettings;
        }

        // We need to check if we can support cancellation in here as well?
        public async Task Dispatch(TransportOperations operations, TransportTransaction transportTransaction, ContextBag context)
        {
            await Dispatch(operations, dispatcher.DispatchAsIsolated, DispatchConsistency.Isolated).ConfigureAwait(false);
            await Dispatch(operations, ops => dispatcher.DispatchAsNonIsolated(ops, transportTransaction), DispatchConsistency.Default).ConfigureAwait(false);
        }

        Task Dispatch(TransportOperations operations, Func<HashSet<MessageWithAddress>, Task> dispatchMethod, DispatchConsistency dispatchConsistency)
        {
            var deduplicatedOperations = new HashSet<MessageWithAddress>();
            foreach (var operation in operations.UnicastTransportOperations)
            {
                if (operation.RequiredDispatchConsistency == dispatchConsistency)
                {
                    var ultimateAddress = addressParser.Parse(operation.Destination);
                    var immediateAddress = locationSettings.GetImmediateAddress(ultimateAddress, operation.Message.Headers);
                    deduplicatedOperations.Add(new MessageWithAddress(operation.Message, ultimateAddress, immediateAddress));
                }
            }
            return dispatchMethod(deduplicatedOperations);
        }

        IQueueDispatcher dispatcher;
        QueueAddressParser addressParser;
        SchemaAndCatalogSettings locationSettings;
    }
}