namespace NServiceBus.Transports.SQLServer
{
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