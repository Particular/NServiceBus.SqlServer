namespace NServiceBus.Transport.SqlServer
{
    using System;

    /// <summary>
    /// Configures native delayed delivery.
    /// </summary>
    public class DelayedDeliveryOptions
    {
        string tableSuffix = "Delayed";
        int batchSize = 100;
        TimeSpan interval = TimeSpan.FromSeconds(1);

        internal DelayedDeliveryOptions() { }

        /// <summary>
        /// Suffix to be appended to the table name storing delayed messages.
        /// </summary>
        public string TableSuffix
        {
            get => tableSuffix;
            set
            {
                Guard.AgainstNullAndEmpty(nameof(tableSuffix), value);
                tableSuffix = value;
            }
        }

        /// <summary>
        /// Size of the batch when moving matured timeouts to the input queue.
        /// </summary>
        public int BatchSize
        {
            get => batchSize;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentException("Batch size has to be a positive number", nameof(batchSize));
                }

                batchSize = value;
            }
        }

        /// <summary>
        /// Configures how often delayed messages are processed (every 5 seconds by default).
        /// </summary>
        public TimeSpan ProcessingInterval
        {
            get => interval;
            set
            {
                Guard.AgainstNegativeAndZero(nameof(interval), value);

                interval = value;
            }
        }
    }
}