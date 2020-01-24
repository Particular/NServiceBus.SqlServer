namespace NServiceBus.Transport.SqlServer
{
    using System;
    using Logging;

    class QueuePeekerOptions
    {
        public QueuePeekerOptions(TimeSpan? delayTime = null)
        {
            var delay = DefaultDelay;

            if (delayTime.HasValue)
            {
                Validate(delayTime.Value);
                delay = delayTime.Value;
            }

            Delay = delay;
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

        public TimeSpan Delay { get; }
        static TimeSpan DefaultDelay = TimeSpan.FromSeconds(1);
        static ILog Logger = LogManager.GetLogger<QueuePeekerOptions>();
    }
}