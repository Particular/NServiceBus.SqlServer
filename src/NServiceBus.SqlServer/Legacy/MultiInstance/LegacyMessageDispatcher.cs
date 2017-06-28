namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Transactions;
    using Extensibility;
    using Transport;

    class LegacyMessageDispatcher : IDispatchMessages
    {
        public LegacyMessageDispatcher(LegacyQueueAddressTranslator addressTranslator, LegacySqlConnectionFactory connectionFactory)
        {
            this.addressTranslator = addressTranslator;
            this.connectionFactory = connectionFactory;
        }

        // We need to check if we can support cancellation in here as well?
        public async Task Dispatch(TransportOperations operations, TransportTransaction transportTransaction, ContextBag context)
        {
            await DeduplicateAndDispatch(operations, DispatchAsIsolated, DispatchConsistency.Isolated).ConfigureAwait(false);
            await DeduplicateAndDispatch(operations, DispatchAsNonIsolated, DispatchConsistency.Default).ConfigureAwait(false);
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

        async Task DispatchAsNonIsolated(List<UnicastTransportOperation> operations)
        {
            //If dispatch is not isolated then either TS has been created by the receive operation or needs to be created here.
            using (var scope = new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled))
            {
                await Dispatch(operations, scope).ConfigureAwait(false);
            }
        }

        async Task DispatchAsIsolated(List<UnicastTransportOperation> operations)
        {
            using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled))
            {
                await Dispatch(operations, scope).ConfigureAwait(false);
            }
        }

        async Task Dispatch(List<UnicastTransportOperation> operations, TransactionScope scope)
        {
            foreach (var operation in operations)
            {
                var address = addressTranslator.Parse(operation.Destination);
                var key = Tuple.Create(address.QualifiedTableName, address.Address);
                var queue = cache.GetOrAdd(key, x => new TableBasedQueue(x.Item1, x.Item2));

                using (var connection = await connectionFactory.OpenNewConnection(address.Address).ConfigureAwait(false))
                {
                    await queue.Send(operation.Message, TimeSpan.MaxValue, connection, null).ConfigureAwait(false);
                }
            }
            scope.Complete();
        }

        LegacySqlConnectionFactory connectionFactory;
        LegacyQueueAddressTranslator addressTranslator;
        ConcurrentDictionary<Tuple<string, string>, TableBasedQueue> cache = new ConcurrentDictionary<Tuple<string, string>, TableBasedQueue>();


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