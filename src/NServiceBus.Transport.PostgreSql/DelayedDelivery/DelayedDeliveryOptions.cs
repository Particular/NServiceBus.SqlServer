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
        /// How long an endpoint instance waits before it tries again to move due delayed messages while another instance holds the lock that moves them.
        /// Must be greater than zero and at most 1 minute. Defaults to 1 second.
        /// </summary>
        /// <remarks>
        /// One endpoint instance at a time moves due delayed messages to the input queue.
        /// </remarks>
        public TimeSpan LockRetryInterval
        {
            get;
            set
            {
                ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(value, TimeSpan.FromMinutes(1));

                field = value;
            }
        } = TimeSpan.FromSeconds(1);
    }
}