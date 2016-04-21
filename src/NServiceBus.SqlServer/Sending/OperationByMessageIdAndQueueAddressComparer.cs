namespace NServiceBus.Transports.SQLServer
{
    using System.Collections.Generic;

    class OperationByMessageIdAndQueueAddressComparer : IEqualityComparer<MessageWithAddress>
    {
        public bool Equals(MessageWithAddress x, MessageWithAddress y)
        {
            return x.Message.MessageId.Equals(y.Message.MessageId)
                   && x.Address.TableName.Equals(y.Address.TableName)
                   && x.Address.SchemaName.Equals(y.Address.SchemaName);
        }

        public int GetHashCode(MessageWithAddress obj)
        {
            unchecked
            {
                var hashCode = obj.Message.MessageId.GetHashCode();
                hashCode = (hashCode * 397) ^ obj.Address.TableName.GetHashCode();
                hashCode = (hashCode * 397) ^ obj.Address.SchemaName.GetHashCode();
                return hashCode;
            }
        }
    }
}