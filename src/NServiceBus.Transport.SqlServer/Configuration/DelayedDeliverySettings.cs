namespace NServiceBus.Transport.SqlServer
{
    using System;
    using Particular.Obsoletes;

    /// <summary>
    /// Configures native delayed delivery.
    /// </summary>
    [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "SqlServerTransport.DelayedDelivery",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
    public partial class DelayedDeliverySettings
    {
        readonly DelayedDeliveryOptions options;

        internal DelayedDeliverySettings(DelayedDeliveryOptions options) => this.options = options;

        /// <summary>
        /// Sets the suffix for the table storing delayed messages.
        /// </summary>
        /// <param name="suffix"></param>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "SqlServerTransport.TableSuffix",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public void TableSuffix(string suffix)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(suffix);

            options.TableSuffix = suffix;
        }

        /// <summary>
        /// Sets the size of the batch when moving matured timeouts to the input queue.
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "SqlServerTransport.BatchSize",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public void BatchSize(int batchSize)
        {
            if (batchSize <= 0)
            {
                throw new ArgumentException("Batch size has to be a positive number", nameof(batchSize));
            }

            options.BatchSize = batchSize;
        }
    }

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
        /// <param name="catalogName">Catalog in which the table is defined if different from default catalog configured for the transport.</param>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "SubscriptionOptions.SubscriptionTableName",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public void SubscriptionTableName(string tableName, string schemaName = null, string catalogName = null) => options.SubscriptionTableName = new SubscriptionTableName(tableName, schemaName, catalogName);

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