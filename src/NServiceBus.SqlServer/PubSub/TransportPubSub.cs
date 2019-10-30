namespace NServiceBus.Transport.SQLServer
{
    using Settings;
    using Unicast.Messages;

    class TransportPubSub
    {
        IManageTransportSubscriptions subscriptions;

        public IManageTransportSubscriptions GetTransportSubscriptionsManager(ReadOnlySettings settings, SqlConnectionFactory connectionFactory)
        {
            if (subscriptions != null)
            {
                return subscriptions;
            }

            subscriptions = new TableBasedSubscriptions(connectionFactory);

            if (settings.TryGet<MessageMetadataRegistry>(out var messageMetadataRegistry))
            {
                subscriptions = new PolymorphicTransportSubscriptionsManagerWrapper(
                    subscriptions,
                    messageMetadataRegistry
                );
            }

            var pubSubSettings = settings.GetOrDefault<TransportPubSubOptions>() ?? new TransportPubSubOptions();

            if (pubSubSettings.TimeToCacheSubscription.HasValue)
            {
                subscriptions = new SubscriptionCache(subscriptions, pubSubSettings.TimeToCacheSubscription.Value);
            }

            return subscriptions;
        }
    }
}