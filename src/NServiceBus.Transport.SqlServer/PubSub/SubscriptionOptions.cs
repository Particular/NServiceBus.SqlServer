namespace NServiceBus.Transport.SqlServer
{
    using System;

    /// <summary>
    ///     Configures the native pub/sub behavior
    /// </summary>
    public class SubscriptionOptions
    {
        /// <summary>
        ///     Default to 5 seconds caching. If a system is under load that prevent doing an extra roundtrip for each Publish
        ///     operation. If
        ///     a system is not under load, doing an extra roundtrip every 5 seconds is not a problem and 5 seconds is small enough
        ///     value that
        ///     people accepts as we always say that subscription operation is not instantaneous.
        /// </summary>
        TimeSpan cacheInvalidationPeriod = TimeSpan.FromSeconds(5);

        /// <summary>
        ///     Subscription table. All endpoints in a given system need to agree on that name in order for them to be able
        ///     to subscribe to and publish events.
        /// </summary>
        public SubscriptionTableName SubscriptionTableName { get; set; } = new SubscriptionTableName("SubscriptionRouting");

        /// <summary>
        ///     Cache subscriptions for a given <see cref="TimeSpan" />.
        /// </summary>
        public TimeSpan CacheInvalidationPeriod
        {
            get => cacheInvalidationPeriod;
            set
            {
                Guard.AgainstNegativeAndZero(nameof(CacheInvalidationPeriod), value);
                cacheInvalidationPeriod = value;
            }
        }

        /// <summary>
        ///     Do not cache subscriptions.
        /// </summary>
        public bool DisableCaching { get; set; } = false;
    }
}