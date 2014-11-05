namespace NServiceBus.Transports.SQLServer
{
    using NServiceBus.Unicast.Queuing;

    class DeadLetterQueueCreator : IWantQueueCreated
    {
        public Address DeadLetterQueueAddress { get; set; }

        public Address Address
        {
            get { return DeadLetterQueueAddress; }
        }

        public bool Enabled { get; set; }

        public bool ShouldCreateQueue()
        {
            return Enabled;
        }
    }
}