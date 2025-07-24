namespace NServiceBus.Transport.SqlServer
{
    using System;

    /// <summary>
    ///     Configures the native pub/sub behavior
    /// </summary>
    public class SubscriptionOptions
    {
        /// <summary>
        ///     Subscription table. All endpoints in a given system need to agree on that name in order for them to be able
        ///     to subscribe to and publish events.
        /// </summary>
        public SubscriptionTableName SubscriptionTableName { get; set; } = new SubscriptionTableName("SubscriptionRouting");

        /// <summary>
        ///     Cache subscriptions for a given <see cref="TimeSpan" />. Defaults to 5 seconds.
        /// </summary>
        public TimeSpan CacheInvalidationPeriod
        {
            get;
            set
            {
                ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);

                field = value;
            }
        } = TimeSpan.FromSeconds(5);

        /// <summary>
        ///     Do not cache subscriptions.
        /// </summary>
        public bool DisableCaching { get; set; } = false;
    }
}