namespace NServiceBus.Transports.SQLServer
{
    class NullQueuePurger :IQueuePurger
    {
        public void Purge(Address address)
        {
            //NOOP
        }
    }
}