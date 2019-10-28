namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    interface IKnowWhereTheSubscriptionsAre
    {
        Task<List<string>> GetSubscribersForEvent(string eventType);
    }
}