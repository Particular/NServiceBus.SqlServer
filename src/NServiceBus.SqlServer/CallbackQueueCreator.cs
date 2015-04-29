namespace NServiceBus.Transports.SQLServer
{
    using NServiceBus.Unicast.Queuing;

    class CallbackQueueCreator : IWantQueueCreated
    {
        public string CallbackQueueAddress { get; set; }

        public string Address
        {
            get { return CallbackQueueAddress; }
        }

        public bool Enabled { get; set; }

        public bool ShouldCreateQueue()
        {
            return Enabled;
        }
    }
}