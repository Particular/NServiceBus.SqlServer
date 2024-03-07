namespace NServiceBus.Transport.Sql.Shared.Sending
{
    using System;
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
        public static SortingResult SortAndDeduplicate(this IEnumerable<UnicastTransportOperation> source, Func<string, string> addressTranslator)
        {
            Dictionary<DeduplicationKey, UnicastTransportOperation> isolatedDispatch = null;
            Dictionary<DeduplicationKey, UnicastTransportOperation> defaultDispatch = null;

            foreach (var operation in source)
            {
                var destination = addressTranslator(operation.Destination);
                var messageId = operation.Message.MessageId;
                var deduplicationKey = new DeduplicationKey(messageId, destination);

                if (operation.RequiredDispatchConsistency == DispatchConsistency.Default)
                {
                    defaultDispatch ??= new Dictionary<DeduplicationKey, UnicastTransportOperation>(DeduplicationKey.MessageIdDestinationComparer);
                    defaultDispatch[deduplicationKey] = operation;
                }
                else if (operation.RequiredDispatchConsistency == DispatchConsistency.Isolated)
                {
                    isolatedDispatch ??= new Dictionary<DeduplicationKey, UnicastTransportOperation>(DeduplicationKey.MessageIdDestinationComparer);
                    isolatedDispatch[deduplicationKey] = operation;
                }
            }

            return new SortingResult(defaultDispatch?.Values, isolatedDispatch?.Values);
        }

        readonly struct DeduplicationKey
        {
            sealed class MessageIdDestinationEqualityComparer : IEqualityComparer<DeduplicationKey>
            {
                public bool Equals(DeduplicationKey x, DeduplicationKey y)
                {
                    return x.messageId == y.messageId && x.destination == y.destination;
                }

                public int GetHashCode(DeduplicationKey obj)
                {
                    unchecked
                    {
                        return (obj.messageId.GetHashCode() * 397) ^ obj.destination.GetHashCode();
                    }
                }
            }

            public static IEqualityComparer<DeduplicationKey> MessageIdDestinationComparer { get; } = new MessageIdDestinationEqualityComparer();

            readonly string messageId;
            readonly string destination;

            public DeduplicationKey(string messageId, string destination)
            {
                this.messageId = messageId;
                this.destination = destination;
            }
        }
    }
}