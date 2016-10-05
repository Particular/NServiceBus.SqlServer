namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;

    class OperationByMessageIdAndQueueAddressComparer : IEqualityComparer<UnicastTransportOperation>
    {
        public bool Equals(UnicastTransportOperation x, UnicastTransportOperation y)
        {
            return x.Message.MessageId.Equals(y.Message.MessageId)
                   && x.Destination.Equals(y.Destination);
        }

        public int GetHashCode(UnicastTransportOperation obj)
        {
            unchecked
            {
                var hashCode = obj.Message.MessageId.GetHashCode();
                hashCode = (hashCode * 397) ^ obj.Destination.GetHashCode();
                return hashCode;
            }
        }
    }
}