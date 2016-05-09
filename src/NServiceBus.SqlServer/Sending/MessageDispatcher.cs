namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Extensibility;
    using Transports;

    class MessageDispatcher : IDispatchMessages
    {
        public MessageDispatcher(IDispatchPolicy dispatchPolicy, QueueAddressParser addressParser)
        {
            this.dispatchPolicy = dispatchPolicy;
            this.addressParser = addressParser;
        }

        // We need to check if we can support cancellation in here as well?
        public async Task Dispatch(TransportOperations operations, ContextBag context)
        {
            await Dispatch(operations, context, DispatchConsistency.Isolated).ConfigureAwait(false);
            await Dispatch(operations, context, DispatchConsistency.Default).ConfigureAwait(false);
        }

        Task Dispatch(TransportOperations operations, ReadOnlyContextBag contextBag, DispatchConsistency dispatchConsistency)
        {
            var isolatedOperations = operations.UnicastTransportOperations.Where(o => o.RequiredDispatchConsistency == dispatchConsistency);
            var deduplicatedIsolatedOperations = DeduplicateBasedOnMessageIdAndQueueAddress(isolatedOperations).ToList();

            var dispatchStrategy = GetDispatchStrategySetByReceiver(contextBag) ?? dispatchPolicy.CreateDispatchStrategy(dispatchConsistency);

            return DispatchUsingStrategy(deduplicatedIsolatedOperations, dispatchStrategy);
        }

        static Task DispatchUsingStrategy(List<MessageWithAddress> operations, IDispatchStrategy dispatchStrategy)
        {
            return dispatchStrategy.Dispatch(operations);
        }

        static IDispatchStrategy GetDispatchStrategySetByReceiver(ReadOnlyContextBag context)
        {
            IDispatchStrategy dispatchStrategy;
            context.TryGet(out dispatchStrategy);
            return dispatchStrategy;
        }

        IEnumerable<MessageWithAddress> DeduplicateBasedOnMessageIdAndQueueAddress(IEnumerable<UnicastTransportOperation> isolatedConsistencyOperations)
        {
            return isolatedConsistencyOperations
                .Select(o => new MessageWithAddress(o.Message, addressParser.Parse(o.Destination)))
                .Distinct(OperationByMessageIdAndQueueAddressComparer);
        }

        IDispatchPolicy dispatchPolicy;
        QueueAddressParser addressParser;
        static OperationByMessageIdAndQueueAddressComparer OperationByMessageIdAndQueueAddressComparer = new OperationByMessageIdAndQueueAddressComparer();
    }
}