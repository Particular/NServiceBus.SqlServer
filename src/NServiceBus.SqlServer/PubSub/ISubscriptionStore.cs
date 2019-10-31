namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    interface ISubscriptionStore
    {
        Task<List<string>> GetSubscribersForTopic(string topic);
        Task Subscribe(string endpointName, string endpointAddress, string topic);
        Task Unsubscribe(string endpointName, string topic);
    }
}