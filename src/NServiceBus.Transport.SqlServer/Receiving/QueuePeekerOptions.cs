namespace NServiceBus.Transport.SqlServer
{
    using System;
    using Logging;

    /// <summary>
    /// SQL Transport queue peeker settings.
    /// </summary>
    public class QueuePeekerOptions
    {
        /// <summary>
        /// Configures queue peeker options.
        /// </summary>
        /// <param name="delayTime">Time delay between peeks.</param>
        /// <param name="maxRecordsToPeek">Maximal number of records to peek.</param>
        public void Configure(TimeSpan? delayTime = null, int? maxRecordsToPeek = null)
        {
            var delay = DefaultDelay;

            if (delayTime.HasValue)
            {
                Validate(delayTime.Value);
                delay = delayTime.Value;
            }

            Delay = delay;

            Validate(maxRecordsToPeek);
            MaxRecordsToPeek = maxRecordsToPeek;
        }

        static void Validate(int? maxRecordsToPeek)
        {
            if (maxRecordsToPeek.HasValue && maxRecordsToPeek < 1)
            {
                var message = "Peek batch size is invalid. The value must be greater than zero.";
                throw new Exception(message);
            }
        }

        static void Validate(TimeSpan delay)
        {
            if (delay < TimeSpan.FromMilliseconds(100))
            {
                var message = "Delay requested is invalid. The value should be greater than 100 ms and less than 10 seconds.";
                throw new Exception(message);
            }

            if (delay > TimeSpan.FromSeconds(10))
            {
                var message = $"Delay requested of {delay} is not recommended. The recommended delay value is between 100 milliseconds to 10 seconds.";
                Logger.Warn(message);
            }
        }

        /// <summary>
        /// Peek delay.
        /// </summary>
        public TimeSpan Delay { get; private set; }

        /// <summary>
        /// Maximal number of records to peek.
        /// </summary>
        public int? MaxRecordsToPeek { get; private set; }

        static TimeSpan DefaultDelay = TimeSpan.FromSeconds(1);
        static ILog Logger = LogManager.GetLogger<QueuePeekerOptions>();
    }
}
