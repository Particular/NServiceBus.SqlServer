namespace NServiceBus.Transport.PostgreSql
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
}