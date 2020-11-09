﻿namespace NServiceBus.Transport.SqlServer
{
    using System;
    using Logging;

    class QueuePeekerOptions
    {
        public QueuePeekerOptions(TimeSpan? delayTime = null, int? maxRecordsToPeek = null)
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
                var message = "Peek batch size is invalid. THe value must be greater than zero.";
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

        public TimeSpan Delay { get; }
        public int? MaxRecordsToPeek { get; }
        static TimeSpan DefaultDelay = TimeSpan.FromSeconds(1);
        static ILog Logger = LogManager.GetLogger<QueuePeekerOptions>();
    }
}