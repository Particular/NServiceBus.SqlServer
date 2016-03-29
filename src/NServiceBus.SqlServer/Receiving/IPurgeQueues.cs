namespace NServiceBus.Transports.SQLServer
{
    using System.Threading.Tasks;

    interface IPurgeQueues
    {
        Task<int> Purge(TableBasedQueue queue);
    }
}