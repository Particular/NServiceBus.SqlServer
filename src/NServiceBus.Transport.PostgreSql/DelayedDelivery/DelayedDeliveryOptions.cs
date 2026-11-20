namespace NServiceBus.Transport.PostgreSql
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

        /// <summary>
        /// Only one endpoint instance at a time moves due delayed messages to the input queue.
        /// Instances that fail to acquire the lock skip the move and retry after this interval. Defaults to 1 second.
        /// </summary>
        public TimeSpan DelayedMessageMoveLockDelay
        {
            get;
            set
            {
                ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(value, TimeSpan.FromMilliseconds(int.MaxValue));

                field = value;
            }
        } = TimeSpan.FromSeconds(1);
    }
}