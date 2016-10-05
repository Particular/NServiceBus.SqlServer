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

        Task Dispatch(TransportOperations operations, Func<UnicastTransportOperation[], Task> dispatchMethod, DispatchConsistency dispatchConsistency)
        {
            var deduplicatedOperations = new Dictionary<DeduplicationKey, UnicastTransportOperation>();
            foreach (var operation in operations.UnicastTransportOperations)
            {
                if (operation.RequiredDispatchConsistency == dispatchConsistency)
                {
                    var queueAddress = addressParser.Parse(operation.Destination);
                    var key = new DeduplicationKey(operation.Message.MessageId, queueAddress.TableName, queueAddress.SchemaName);
                    deduplicatedOperations[key] = operation;
                }
            }
            return dispatchMethod(deduplicatedOperations.Values.ToArray());
        }

        IQueueDispatcher dispatcher;
        QueueAddressParser addressParser;

        sealed class DeduplicationKey
        {
            string messageId;
            string table;
            string schema;

            public DeduplicationKey(string messageId, string table, string schema)
            {
                this.messageId = messageId;
                this.table = table;
                this.schema = schema;
            }

            bool Equals(DeduplicationKey other)
            {
                return string.Equals(messageId, other.messageId) && string.Equals(table, other.table) && string.Equals(schema, other.schema);
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
                    var hashCode = messageId?.GetHashCode() ?? 0;
                    hashCode = (hashCode*397) ^ (table?.GetHashCode() ?? 0);
                    hashCode = (hashCode*397) ^ (schema?.GetHashCode() ?? 0);
                    return hashCode;
                }
            }
        }
    }
}