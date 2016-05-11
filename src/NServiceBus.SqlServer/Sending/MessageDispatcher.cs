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
            await Dispatch(operations, DispatchConsistency.Isolated, dispatchPolicy.CreateDispatchStrategy(DispatchConsistency.Isolated)).ConfigureAwait(false);
            await Dispatch(operations, DispatchConsistency.Default, GetDispatchStrategySetByReceiver(context) ?? dispatchPolicy.CreateDispatchStrategy(DispatchConsistency.Default)).ConfigureAwait(false);
        }

        async Task Dispatch(TransportOperations operations, DispatchConsistency dispatchConsistency, IDispatchStrategy dispatchStrategy)
        {
            var isolatedOperations = operations.UnicastTransportOperations.Where(o => o.RequiredDispatchConsistency == dispatchConsistency);
            var deduplicatedIsolatedOperations = DeduplicateBasedOnMessageIdAndQueueAddress(isolatedOperations).ToList();
            if (deduplicatedIsolatedOperations.Count == 0)
            {
                return;
            }

            await DispatchUsingStrategy(deduplicatedIsolatedOperations, dispatchStrategy).ConfigureAwait(false);
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