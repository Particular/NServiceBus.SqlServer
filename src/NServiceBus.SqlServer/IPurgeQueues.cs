namespace NServiceBus.Transports.SQLServer
{
    interface IPurgeQueues
    {
        void Purge(Address address);
    }

    class NullQueuePurger : IPurgeQueues
    {
        public void Purge(Address address)
        {
        }
    }
}