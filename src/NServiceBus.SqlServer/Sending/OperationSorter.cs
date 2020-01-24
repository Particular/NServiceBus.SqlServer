namespace NServiceBus.Transport.SqlServer
{
    using System.Collections.Generic;

    struct SortingResult
    {
        public IEnumerable<UnicastTransportOperation> IsolatedDispatch;
        public IEnumerable<UnicastTransportOperation> DefaultDispatch;

        public SortingResult(IEnumerable<UnicastTransportOperation> defaultDispatch, IEnumerable<UnicastTransportOperation> isolatedDispatch)
        {
            DefaultDispatch = defaultDispatch;
            IsolatedDispatch = isolatedDispatch;
        }
    }

    static class OperationSorter
    {
        public static SortingResult SortAndDeduplicate(this IEnumerable<UnicastTransportOperation> source, QueueAddressTranslator addressTranslator)
        {
            Dictionary<DeduplicationKey, UnicastTransportOperation> isolatedDispatch = null;
            Dictionary<DeduplicationKey, UnicastTransportOperation> defaultDispatch = null;

            foreach (var operation in source)
            {
                var destination = addressTranslator.Parse(operation.Destination).Address;
                var messageId = operation.Message.MessageId;
                var deduplicationKey = new DeduplicationKey(messageId, destination);

                if (operation.RequiredDispatchConsistency == DispatchConsistency.Default)
                {
                    if (defaultDispatch == null)
                    {
                        defaultDispatch = new Dictionary<DeduplicationKey, UnicastTransportOperation>();
                    }
                    defaultDispatch[deduplicationKey] = operation;
                }
                else if (operation.RequiredDispatchConsistency == DispatchConsistency.Isolated)
                {
                    if (isolatedDispatch == null)
                    {
                        isolatedDispatch = new Dictionary<DeduplicationKey, UnicastTransportOperation>();
                    }
                    isolatedDispatch[deduplicationKey] = operation;
                }
            }

            return new SortingResult(defaultDispatch?.Values, isolatedDispatch?.Values);
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
                return Equals((DeduplicationKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (messageId.GetHashCode() * 397) ^ destination.GetHashCode();
                }
            }
        }
    }
}