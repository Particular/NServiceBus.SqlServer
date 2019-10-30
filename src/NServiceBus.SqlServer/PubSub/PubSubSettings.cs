namespace NServiceBus.Transport.SQLServer
{
    using System;
    using Settings;

    /// <summary>
    /// Configures the native pub/sub behavior
    /// </summary>
    public class PubSubSettings
    {
        internal SubscriptionTableName SubscriptionTable = new SubscriptionTableName("SubscriptionRouting", null, null);
        TimeSpan? TimeToCacheSubscriptions;

        /// <summary>
        /// Overrides the default name for the subscription table. All endpoints in a given system need to agree on that name in order for them to be able
        /// to subscribe to and publish events.
        /// </summary>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="schemaName">Schema in which the table is defined if different from default schema configured for the transport.</param>
        /// <param name="catalogName">Catalog in which the table is defined if different from default catalog configured for the transport.</param>
        public void SubscriptionTableName(string tableName, string schemaName = null, string catalogName = null)
        {
            SubscriptionTable = new SubscriptionTableName(tableName, schemaName, catalogName);
        }

        /// <summary>
        /// Cache subscriptions for a given <see cref="TimeSpan"/>.
        /// </summary>
        public void CacheSubscriptionInformationFor(TimeSpan timeSpan)
        {
            Guard.AgainstNegativeAndZero(nameof(timeSpan), timeSpan);
            TimeToCacheSubscriptions = timeSpan;
        }

        /// <summary>
        /// Do not cache subscriptions.
        /// </summary>
        public void DisableSubscriptionCache()
        {
            TimeToCacheSubscriptions = TimeSpan.Zero;
        }

        internal TimeSpan? GetCacheFor()
        {
            if (TimeToCacheSubscriptions == TimeSpan.Zero)
            {
                return null;
            }

            if (TimeToCacheSubscriptions.HasValue)
            {
                return TimeToCacheSubscriptions.Value;
            }
            throw new Exception(@"Subscription caching is a required settings. Access this setting using the following:
var transport = endpointConfiguration.UseTransport<SqlServerTransport>();
var pubsub = transport.PuSub();
subscriptions.CacheSubscriptionInformationFor(TimeSpan.FromMinutes(1));
// OR
subscriptions.DisableSubscriptionCache();
");
        }
    }
}