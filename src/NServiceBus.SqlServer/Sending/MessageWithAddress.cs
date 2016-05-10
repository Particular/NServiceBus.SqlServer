namespace NServiceBus.Transport.SQLServer
{
    using Transports;

    class MessageWithAddress
    {
        public QueueAddress Address { get; }
        public OutgoingMessage Message { get; }

        public MessageWithAddress(OutgoingMessage message, QueueAddress address)
        {
            Address = address;
            Message = message;
        }
    }
}