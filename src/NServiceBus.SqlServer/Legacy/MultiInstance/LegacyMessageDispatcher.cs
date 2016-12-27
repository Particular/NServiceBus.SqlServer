namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Extensibility;
    using Transport;

    class LegacyMessageDispatcher : IDispatchMessages
    {
        public LegacyMessageDispatcher(IQueueDispatcher dispatcher, LegacyQueueAddressTranslator addressTranslator)
        {
            this.dispatcher = dispatcher;
            this.addressTranslator = addressTranslator;
        }

        // We need to check if we can support cancellation in here as well?
        public async Task Dispatch(TransportOperations operations, TransportTransaction transportTransaction, ContextBag context)
        {
            await DeduplicateAndDispatch(operations, dispatcher.DispatchAsIsolated, DispatchConsistency.Isolated).ConfigureAwait(false);
            await DeduplicateAndDispatch(operations, ops => dispatcher.DispatchAsNonIsolated(ops, transportTransaction), DispatchConsistency.Default).ConfigureAwait(false);
        }

        Task DeduplicateAndDispatch(TransportOperations operations, Func<List<UnicastTransportOperation>, Task> dispatchMethod, DispatchConsistency dispatchConsistency)
        {
            var operationsToDispatch = operations.UnicastTransportOperations
                .Where(o => o.RequiredDispatchConsistency == dispatchConsistency)
                .GroupBy(o => new DeduplicationKey(o.Message.MessageId, addressTranslator.Parse(o.Destination).Address))
                .Select(g => g.First())
                .ToList();

            return dispatchMethod(operationsToDispatch);
        }

        IQueueDispatcher dispatcher;
        LegacyQueueAddressTranslator addressTranslator;

        class DeduplicationKey
        {
            string messageId;
            string destination;

            public DeduplicationKey(string messageId, string destination)
            {
                this.messageId = messageId;
                this.destination = destination;
            }

            bool Equals(DeduplicationKey other)
            {
                return string.Equals(messageId, other.messageId) && string.Equals(destination, other.destination);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((DeduplicationKey) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (messageId.GetHashCode()*397) ^ destination.GetHashCode();
                }
            }
        }
    }
}