namespace NServiceBus.Transports.SQLServer
{
    interface IQueuePurger
    {
        void Purge(string address);
    }
}