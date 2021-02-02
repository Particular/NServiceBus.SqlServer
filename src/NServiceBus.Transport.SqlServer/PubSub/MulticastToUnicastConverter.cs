namespace NServiceBus.Transport.SqlServer
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    class MulticastToUnicastConverter : IMulticastToUnicastConverter
    {
        ISubscriptionStore subscriptions;

        public MulticastToUnicastConverter(ISubscriptionStore subscriptions) => this.subscriptions = subscriptions;

        public async Task<List<UnicastTransportOperation>> Convert(MulticastTransportOperation transportOperation)
        {
            List<string> subscribers =
                await subscriptions.GetSubscribers(transportOperation.MessageType).ConfigureAwait(false);

            return (from subscriber in subscribers
                    select new UnicastTransportOperation(
                        transportOperation.Message,
                        subscriber,
                        transportOperation.Properties,
                        transportOperation.RequiredDispatchConsistency
                )).ToList();
        }
    }
}