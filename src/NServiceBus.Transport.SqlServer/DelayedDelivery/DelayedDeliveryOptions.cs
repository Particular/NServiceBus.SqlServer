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
        /// Configures how often delayed messages are processed.
        /// </summary>
        [ObsoleteEx(Message = "Delayed message polling now uses an adaptive delays and no longer needs a processing interval. This setting is safe to remove.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public TimeSpan ProcessingInterval
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }
    }
}