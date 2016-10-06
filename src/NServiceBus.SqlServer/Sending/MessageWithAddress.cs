namespace NServiceBus.Transport.SQLServer
{
    using System;
    using Transport;

    class MessageWithAddress
    {
        public QueueAddress Address { get; }
        public OutgoingMessage Message { get; }
        public DateTime? Due { get; }

        public MessageWithAddress(OutgoingMessage message, QueueAddress address, DateTime? due)
        {
            Address = address;
            Due = due;
            Message = message;
        }
    }
}