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
        public MessageDispatcher(IQueueDispatcher dispatcher, QueueAddressTranslator addressTranslator, ISubscriptionStore subscriptions)
        {
            this.dispatcher = dispatcher;
            this.addressTranslator = addressTranslator;
            this.subscriptions = subscriptions;
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
            var tasks = operations.MulticastTransportOperations.Select(m => ConvertToUnicastOperations(m)).ToArray();
            await Task.WhenAll(tasks).ConfigureAwait(false);
            return tasks.SelectMany(t => t.Result).ToList();
        }

        async Task<List<UnicastTransportOperation>> ConvertToUnicastOperations(MulticastTransportOperation transportOperation)
        {
            var destinations = await subscriptions.GetSubscribersForTopic(TopicName(transportOperation.MessageType)).ConfigureAwait(false);

            return (from destination in destinations
                    select new UnicastTransportOperation(
                        transportOperation.Message,
                        destination,
                        transportOperation.RequiredDispatchConsistency,
                        transportOperation.DeliveryConstraints
                    )).ToList();
        }

        IQueueDispatcher dispatcher;
        QueueAddressTranslator addressTranslator;
        ISubscriptionStore subscriptions;

        static string TopicName(Type type)
        {
            return $"{type.Namespace}.{type.Name}";
        }


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