namespace NServiceBus
{
    using System;
    using Logging;

    /// <summary>
    /// Queue peeker options.
    /// </summary>
    public class QueuePeekerOptions
    {
        internal QueuePeekerOptions() { }

        /// <summary>
        /// Time delay between peeks.
        /// </summary>
        public TimeSpan Delay
        {
            get => delay;
            set
            {
                if (value < TimeSpan.FromMilliseconds(100))
                {
                    var message = "Delay requested is invalid. The value should be greater than 100 ms and less than 10 seconds.";
                    throw new Exception(message);
                }

                if (value > TimeSpan.FromSeconds(10))
                {
                    var message = $"Delay requested of {value} is not recommended. The recommended delay value is between 100 milliseconds to 10 seconds.";
                    Logger.Warn(message);
                }

                delay = value;
            }
        }

        /// <summary>
        /// Maximal number of records to peek.
        /// </summary>
        public int? MaxRecordsToPeek
        {
            get => maxRecordsToPeek;
            set
            {
                if (value.HasValue && value < 1)
                {
                    var message = "Peek batch size is invalid. The value must be greater than zero.";
                    throw new Exception(message);
                }

                maxRecordsToPeek = value;
            }
        }

        TimeSpan delay = TimeSpan.FromSeconds(1);
        int? maxRecordsToPeek;

        static ILog Logger = LogManager.GetLogger<QueuePeekerOptions>();
    }
}
