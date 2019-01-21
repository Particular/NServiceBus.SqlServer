namespace NServiceBus.Transport.SQLServer
{
    using System;

    /// <summary>
    /// Configures native delayed delivery.
    /// </summary>
    public class DelayedDeliverySettings
    {
        internal string Suffix = "Delayed";
        internal TimeSpan Interval = TimeSpan.FromSeconds(1);
        internal bool EnableMigrationMode = true;
        internal int MatureBatchSize = 100;

        /// <summary>
        /// Sets the suffix for the table storing delayed messages.
        /// </summary>
        /// <param name="suffix"></param>
        public void TableSuffix(string suffix)
        {
            Guard.AgainstNullAndEmpty(nameof(suffix), suffix);
            Suffix = suffix;
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
            MatureBatchSize = batchSize;
        }

        /// <summary>
        /// Disables the Timeout Manager for the endpoint. Before disabling ensure there all timeouts in the timeout store have been processed or migrated.
        /// </summary>
        public void DisableTimeoutManagerCompatibility()
        {
            EnableMigrationMode = false;
        }

        /// <summary>
        /// Configures how often delayed messages are processed (every 5 seconds by default).
        /// </summary>
        public void ProcessingInterval(TimeSpan interval)
        {
            Guard.AgainstNegativeAndZero(nameof(interval), interval);
            Interval = interval;
        }
    }
}