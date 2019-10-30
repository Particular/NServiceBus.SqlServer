namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Unicast.Messages;

    class PolymorphicTransportSubscriptionsManagerWrapper : IManageTransportSubscriptions
    {
        public PolymorphicTransportSubscriptionsManagerWrapper(IManageTransportSubscriptions inner, MessageMetadataRegistry messageMetadataRegistry)
        {
            this.inner = inner;
            this.messageMetadataRegistry = messageMetadataRegistry;
        }

        public async Task<List<string>> GetSubscribersForEvent(Type eventType)
        {
            var messageMetadata = messageMetadataRegistry.GetMessageMetadata(eventType);

            var tasks = messageMetadata.MessageHierarchy.Select(t => inner.GetSubscribersForEvent(t)).ToArray();

            await Task.WhenAll(tasks).ConfigureAwait(false);

            var results = tasks.SelectMany(t => t.Result).Distinct().ToList();

            return results;
        }

        public Task Subscribe(string endpointName, string endpointAddress, Type eventType)
        {
            return inner.Subscribe(endpointName, endpointAddress, eventType);
        }

        public Task Unsubscribe(string endpointName, Type eventType)
        {
            return inner.Unsubscribe(endpointName, eventType);
        }

        MessageMetadataRegistry messageMetadataRegistry;
        IManageTransportSubscriptions inner;

    }
}