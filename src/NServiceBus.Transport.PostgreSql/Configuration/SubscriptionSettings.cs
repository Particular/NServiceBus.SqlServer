namespace NServiceBus.Transport.PostgreSql
{
    using System;
    using Particular.Obsoletes;

    /// <summary>
    /// Configures the native pub/sub behavior
    /// </summary>
    [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "SqlServerTransport.Subscriptions",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
    public class SubscriptionSettings
    {
        readonly SubscriptionOptions options;

        internal SubscriptionSettings(SubscriptionOptions options) => this.options = options;

        /// <summary>
        /// Overrides the default name for the subscription table. All endpoints in a given system need to agree on that name in order for them to be able
        /// to subscribe to and publish events.
        /// </summary>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="schemaName">Schema in which the table is defined if different from default schema configured for the transport.</param>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "SubscriptionOptions.SubscriptionTableName",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public void SubscriptionTableName(string tableName, string schemaName = null) => options.SubscriptionTableName = new SubscriptionTableName(tableName, schemaName);

        /// <summary>
        /// Cache subscriptions for a given <see cref="TimeSpan"/>.
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public void CacheSubscriptionInformationFor(TimeSpan timeSpan)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeSpan, TimeSpan.Zero);
            options.CacheInvalidationPeriod = timeSpan;
        }

        /// <summary>
        /// Do not cache subscriptions.
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "SubscriptionOptions.DisableSubscriptionCache",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public void DisableSubscriptionCache() => options.DisableCaching = true;
    }
}

