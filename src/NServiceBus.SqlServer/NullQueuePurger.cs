namespace NServiceBus.Transports.SQLServer
{
    class NullQueuePurger :IQueuePurger
    {
        public void Purge(string address)
        {
            //NOOP
        }
    }
}