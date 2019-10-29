namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    interface IManageTransportSubscriptions
    {
        Task<List<string>> GetSubscribersForEvent(string eventType);
        Task Subscribe(string endpointName, string endpointAddress, string eventType);
        Task Unsubscribe(string endpointName, string eventType);
    }
}