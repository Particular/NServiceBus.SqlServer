namespace NServiceBus.Transports.SQLServer
{
    interface IQueuePurger
    {
        void Purge(Address address);
    }
}