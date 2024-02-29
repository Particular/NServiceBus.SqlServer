namespace NServiceBus.Transport.SqlServer
{
    using System;

    /// <summary>
    /// Configures native delayed delivery.
    /// </summary>
    public partial class DelayedDeliveryOptions
    {
        string tableSuffix = "Delayed";
        int batchSize = 100;

        internal DelayedDeliveryOptions() { }

        /// <summary>
        /// Suffix to be appended to the table name storing delayed messages.
        /// </summary>
        public string TableSuffix
        {
            get => tableSuffix;
            set
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(value);

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
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);

                batchSize = value;
            }
        }
    }
}