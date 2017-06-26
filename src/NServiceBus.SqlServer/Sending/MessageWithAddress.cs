namespace NServiceBus.Transport.SQLServer
{
    using System;
    using Transport;

    class MessageWithAddress
    {
        public QueueAddress Address { get; }
        public OutgoingMessage Message { get; }
        public TimeSpan? TimeToBeReceived { get; }

        public MessageWithAddress(OutgoingMessage message, QueueAddress address, TimeSpan? timeToBeReceived)
        {
            Address = address;
            TimeToBeReceived = timeToBeReceived;
            Message = message;
        }
    }
}