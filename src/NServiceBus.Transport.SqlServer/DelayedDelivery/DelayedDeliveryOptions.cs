namespace NServiceBus.Transport.SqlServer
{
    using System;

    /// <summary>
    /// Configures native delayed delivery.
    /// </summary>
    public partial class DelayedDeliveryOptions
    {
        internal DelayedDeliveryOptions() { }

        /// <summary>
        /// Suffix to be appended to the table name storing delayed messages.
        /// </summary>
        public string TableSuffix
        {
            get;
            set
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(value);

                field = value;
            }
        } = "Delayed";

        /// <summary>
        /// Size of the batch when moving matured timeouts to the input queue.
        /// </summary>
        public int BatchSize
        {
            get;
            set
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);

                field = value;
            }
        } = 100;
    }
}