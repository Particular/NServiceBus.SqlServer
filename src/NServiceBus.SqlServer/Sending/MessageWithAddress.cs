namespace NServiceBus.Transport.SQLServer
{
    using Transport;

    class MessageWithAddress
    {
        QueueAddress UltimateAddress { get; }
        public QueueAddress Address { get; }
        public OutgoingMessage Message { get; }

        public MessageWithAddress(OutgoingMessage message, QueueAddress ultimateAddress, QueueAddress address)
        {
            UltimateAddress = ultimateAddress;
            Address = address;
            Message = message;
        }

        public override bool Equals(object obj)
        {
            var other = obj as MessageWithAddress;
            if (other == null)
            {
                return false;
            }
            return Equals(this, other);
        }

        public override int GetHashCode()
        {
            return GetHashCode(this);
        }

        bool Equals(MessageWithAddress x, MessageWithAddress y)
        {
            return x.Message.MessageId.Equals(y.Message.MessageId)
                   && x.UltimateAddress.TableName.Equals(y.UltimateAddress.TableName)
                   && x.UltimateAddress.SchemaName.Equals(y.UltimateAddress.SchemaName);
        }

        int GetHashCode(MessageWithAddress obj)
        {
            unchecked
            {
                var hashCode = obj.Message.MessageId.GetHashCode();
                hashCode = (hashCode * 397) ^ obj.UltimateAddress.TableName.GetHashCode();
                hashCode = (hashCode * 397) ^ obj.UltimateAddress.SchemaName.GetHashCode();
                return hashCode;
            }
        }
    }
}