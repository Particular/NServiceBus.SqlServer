namespace NServiceBus.Transport.SqlServer
{
    using System;

    /// <summary>
    /// Configures the native pub/sub behavior
    /// </summary>
    public class SubscriptionSettings
    {
        internal SubscriptionTableName SubscriptionTable = new SubscriptionTableName("SubscriptionRouting", null, null);

        /// <summary>
        /// Default to 5 seconds caching. If a system is under load that prevent doing an extra roundtrip for each Publish operation. If
        /// a system is not under load, doing an extra roundtrip every 5 seconds is not a problem and 5 seconds is small enough value that
        /// people accepts as we always say that subscription operation is not instantaneous.
        /// </summary>
        internal TimeSpan? TimeToCacheSubscriptions = TimeSpan.FromSeconds(5);

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
            TimeToCacheSubscriptions = null;
        }
    }
}