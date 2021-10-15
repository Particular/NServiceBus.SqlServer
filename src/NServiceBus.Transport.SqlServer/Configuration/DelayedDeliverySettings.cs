namespace NServiceBus.Transport.SqlServer
{
    using System;

    /// <summary>
    /// Configures native delayed delivery.
    /// </summary>
    [PreObsolete(Message = "DelayedDeliverySettings has been obsoleted.",
        ReplacementTypeOrMember = "SqlServerTransport.DelayedDelivery", RemoveInVersion = "9.0", TreatAsErrorFromVersion = "8.0")]
    public partial class DelayedDeliverySettings
    {
        DelayedDeliveryOptions options;

        internal DelayedDeliverySettings(DelayedDeliveryOptions options) => this.options = options;

        /// <summary>
        /// Sets the suffix for the table storing delayed messages.
        /// </summary>
        /// <param name="suffix"></param>
        [PreObsolete(ReplacementTypeOrMember = "SqlServerTransport.TableSuffix", RemoveInVersion = "9.0", TreatAsErrorFromVersion = "8.0")]
        public void TableSuffix(string suffix)
        {
            Guard.AgainstNullAndEmpty(nameof(suffix), suffix);

            options.TableSuffix = suffix;
        }

        /// <summary>
        /// Sets the size of the batch when moving matured timeouts to the input queue.
        /// </summary>
        [PreObsolete(ReplacementTypeOrMember = "SqlServerTransport.BatchSize", RemoveInVersion = "9.0", TreatAsErrorFromVersion = "8.0")]
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
    [PreObsolete(Message = "SubscriptionSettings has been obsoleted.",
        ReplacementTypeOrMember = "SqlServerTransport.Subscriptions", RemoveInVersion = "9.0", TreatAsErrorFromVersion = "8.0")]
    public class SubscriptionSettings
    {
        SubscriptionOptions options;

        internal SubscriptionSettings(SubscriptionOptions options) => this.options = options;

        /// <summary>
        /// Overrides the default name for the subscription table. All endpoints in a given system need to agree on that name in order for them to be able
        /// to subscribe to and publish events.
        /// </summary>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="schemaName">Schema in which the table is defined if different from default schema configured for the transport.</param>
        /// <param name="catalogName">Catalog in which the table is defined if different from default catalog configured for the transport.</param>
        [PreObsolete(ReplacementTypeOrMember = "SubscriptionOptions.SubscriptionTableName", RemoveInVersion = "9.0", TreatAsErrorFromVersion = "8.0")]
        public void SubscriptionTableName(string tableName, string schemaName = null, string catalogName = null) => options.SubscriptionTableName = new SubscriptionTableName(tableName, schemaName, catalogName);

        /// <summary>
        /// Cache subscriptions for a given <see cref="TimeSpan"/>.
        /// </summary>
        [PreObsolete(ReplacementTypeOrMember = "SubscriptionOptions.CacheSubscriptionInformationFor", RemoveInVersion = "9.0", TreatAsErrorFromVersion = "8.0")]
        public void CacheSubscriptionInformationFor(TimeSpan timeSpan)
        {
            Guard.AgainstNegativeAndZero(nameof(timeSpan), timeSpan);
            options.CacheInvalidationPeriod = timeSpan;
        }

        /// <summary>
        /// Do not cache subscriptions.
        /// </summary>
        [PreObsolete(ReplacementTypeOrMember = "SubscriptionOptions.DisableSubscriptionCache", RemoveInVersion = "9.0", TreatAsErrorFromVersion = "8.0")]
        public void DisableSubscriptionCache() => options.DisableCaching = true;
    }
}