namespace NServiceBus.Transport.SqlServer
{
    using System;
    using Configuration.AdvancedExtensibility;
    using Settings;

    /// <summary>
    /// Configures native delayed delivery.
    /// </summary>
    public partial class DelayedDeliverySettings : ExposeSettings
    {
        internal DelayedDeliverySettings(SettingsHolder settings) : base(settings) { }

        /// <summary>
        /// Sets the suffix for the table storing delayed messages.
        /// </summary>
        /// <param name="suffix">The suffix to be appended</param>
        public void TableSuffix(string suffix)
        {
            Guard.AgainstNullAndEmpty(nameof(suffix), suffix);

            this.GetSettings().Set(SettingsKeys.DelayedDeliverySuffix, suffix);
        }

        /// <summary>
        /// Sets the size of the batch when moving matured timeouts to the input queue.
        /// </summary>
        public void BatchSize(int batchSize)
        {
            if (batchSize <= 0)
            {
                throw new ArgumentException("Batch size has to be a positive number", nameof(batchSize));
            }

            this.GetSettings().Set(SettingsKeys.DelayedDeliveryMatureBatchSize, batchSize);
        }

        /// <summary>
        /// Configures how often delayed messages are processed (every 5 seconds by default).
        /// </summary>
        public void ProcessingInterval(TimeSpan interval)
        {
            Guard.AgainstNegativeAndZero(nameof(interval), interval);

            this.GetSettings().Set(SettingsKeys.DelayedDeliveryInterval, interval);
        }
    }
}