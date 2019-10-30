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

            var pubSubSettings = settings.GetOrDefault<TransportPubSubOptions>() ?? new TransportPubSubOptions();
            var messageMetadataRegistry = settings.Get<MessageMetadataRegistry>();

            subscriptions = new PolymorphicTransportSubscriptionsManagerWrapper(
                new TableBasedSubscriptions(connectionFactory),
                messageMetadataRegistry
            );

            if (pubSubSettings.TimeToCacheSubscription.HasValue)
            {
                subscriptions = new SubscriptionCache(subscriptions, pubSubSettings.TimeToCacheSubscription.Value);
            }

            return subscriptions;

        }
    }
}