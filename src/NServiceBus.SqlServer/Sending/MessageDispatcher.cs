namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Extensibility;
    using Transport;

    class MessageDispatcher : IDispatchMessages
    {
        public MessageDispatcher(IQueueDispatcher dispatcher, QueueAddressTranslator addressTranslator, IMulticastToUnicastConverter multicastToUnicastConverter)
        {
            this.dispatcher = dispatcher;
            this.addressTranslator = addressTranslator;
            this.multicastToUnicastConverter = multicastToUnicastConverter;
        }

        // We need to check if we can support cancellation in here as well?
        public async Task Dispatch(TransportOperations operations, TransportTransaction transportTransaction, ContextBag context)
        {
            await DeduplicateAndDispatch(operations.UnicastTransportOperations, dispatcher.DispatchAsIsolated, DispatchConsistency.Isolated).ConfigureAwait(false);
            await DeduplicateAndDispatch(operations.UnicastTransportOperations, ops => dispatcher.DispatchAsNonIsolated(ops, transportTransaction), DispatchConsistency.Default).ConfigureAwait(false);

            var multicastOperations = await ConvertToUnicastOperations(operations).ConfigureAwait(false);
            await DeduplicateAndDispatch(multicastOperations, dispatcher.DispatchAsIsolated, DispatchConsistency.Isolated).ConfigureAwait(false);
            await DeduplicateAndDispatch(multicastOperations, ops => dispatcher.DispatchAsNonIsolated(ops, transportTransaction), DispatchConsistency.Default).ConfigureAwait(false);
        }

        Task DeduplicateAndDispatch(IEnumerable<UnicastTransportOperation> transportOperations, Func<List<UnicastTransportOperation>, Task> dispatchMethod, DispatchConsistency dispatchConsistency)
        {
            var operationsToDispatch = transportOperations
                .Where(o => o.RequiredDispatchConsistency == dispatchConsistency)
                .GroupBy(o => new DeduplicationKey(o.Message.MessageId, addressTranslator.Parse(o.Destination).Address))
                .Select(g => g.First())
                .ToList();

            return dispatchMethod(operationsToDispatch);
        }

        async Task<List<UnicastTransportOperation>> ConvertToUnicastOperations(TransportOperations operations)
        {
            var tasks = operations.MulticastTransportOperations.Select(multicastToUnicastConverter.Convert).ToArray();
            await Task.WhenAll(tasks).ConfigureAwait(false);
            return tasks.SelectMany(t => t.Result).ToList();
        }

        IQueueDispatcher dispatcher;
        QueueAddressTranslator addressTranslator;
        IMulticastToUnicastConverter multicastToUnicastConverter;

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
                if (ReferenceEquals(null, obj))
                {
                    return false;
                }
                if (ReferenceEquals(this, obj))
                {
                    return true;
                }
                if (obj.GetType() != this.GetType())
                {
                    return false;
                }
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