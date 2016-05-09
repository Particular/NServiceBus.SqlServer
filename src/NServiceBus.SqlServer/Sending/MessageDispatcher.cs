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
            await Dispatch(operations, DispatchUsingStrategy, DispatchConsistency.Isolated, GetDispatchStrategySetByReceiver(context) ?? dispatcher.CreateIsolatedDispatchStrategy()).ConfigureAwait(false);
            await Dispatch(operations, DispatchUsingStrategy, DispatchConsistency.Default, GetDispatchStrategySetByReceiver(context) ?? dispatcher.CreateNonIsolatedDispatchStrategy()).ConfigureAwait(false);
        }

        static IDispatchStrategy GetDispatchStrategySetByReceiver(ContextBag context)
        {
            TransportTransaction transportTransaction;
            return context.TryGet(out transportTransaction) 
                ? transportTransaction.Get<IDispatchStrategy>() 
                : null;
        }

        Task DispatchUsingStrategy(List<MessageWithAddress> operations, IDispatchStrategy dispatchStrategy)
        {
            return dispatchStrategy.Dispatch(operations);
        }

        Task Dispatch(TransportOperations operations, Func<List<MessageWithAddress>, IDispatchStrategy, Task> dispatchMethod, DispatchConsistency dispatchConsistency, IDispatchStrategy dispatchStrategy)
        {
            var isolatedOperations = operations.UnicastTransportOperations.Where(o => o.RequiredDispatchConsistency == dispatchConsistency);
            var deduplicatedIsolatedOperations = DeduplicateBasedOnMessageIdAndQueueAddress(isolatedOperations).ToList();
            return dispatchMethod(deduplicatedIsolatedOperations, dispatchStrategy);
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