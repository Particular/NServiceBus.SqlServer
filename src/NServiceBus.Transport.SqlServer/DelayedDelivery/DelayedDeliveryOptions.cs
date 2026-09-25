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

        /// <summary>
        /// When set, only one endpoint instance at a time moves due delayed messages to the input queue.
        /// Instances that fail to acquire the lock skip the move and check again after this delay.
        ///
        /// When <c>null</c> (the default), every instance moves due delayed messages independently.
        /// </summary>
        public TimeSpan? DelayedMessageMoveLockDelay
        {
            get;
            set
            {
                if (value.HasValue)
                {
                    ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value.Value, TimeSpan.Zero);
                    ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Value, TimeSpan.FromMilliseconds(int.MaxValue));
                }

                field = value;
            }
        }
    }
}