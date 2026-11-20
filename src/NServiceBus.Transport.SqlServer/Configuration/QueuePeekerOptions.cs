namespace NServiceBus
{
    using System;
    using Logging;
    using Particular.Obsoletes;

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
            get;
            set
            {
                if (value < TimeSpan.FromMilliseconds(100))
                {
                    var message =
                        "Delay requested is invalid. The value should be greater than 100 ms and less than 10 seconds.";
                    throw new Exception(message);
                }

                if (value > TimeSpan.FromSeconds(10))
                {
                    var message =
                        $"Delay requested of {value} is not recommended. The recommended delay value is between 100 milliseconds to 10 seconds.";
                    Logger.Warn(message);
                }

                field = value;
            }
        } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Anchors receives past the contended head of the queue, reducing database load with many competing receivers
        /// </summary>
        /// <remarks>
        /// Rows left behind the anchor are picked up by sweeping from the head of the queue every <see cref="HeadSweepInterval"/>.
        /// </remarks>
        public bool EnableReceiveAnchoring { get; set; }

        /// <summary>
        /// Anchors the peek past the contended head of the queue while messages keep arriving.
        /// Only used when <see cref="EnableReceiveAnchoring"/> is set
        /// </summary>
        /// <remarks>
        /// After the peek delay, when the previous batch received nothing, the peek reads the whole queue
        /// so rows left behind the anchor are still found.
        /// </remarks>
        public bool EnablePeekAnchoring { get; set; }

        /// <summary>
        /// Interval between receives sweeping the whole queue from its head, to pick up rows that are left behind the anchor
        /// Only used when <see cref="EnableReceiveAnchoring"/> is set
        /// </summary>
        /// <remarks>
        /// A row may be left behind if message handling has errors, because of commit delays, or other instances of the endpoint
        /// commit out of order. Values below 1 second are clamped to 1 second. For best performance it should be higher than the
        /// peek delay, we recommend 5-10x the peek delay, but no more than a few seconds.
        /// </remarks>
        public TimeSpan HeadSweepInterval
        {
            get;
            set => field = value < MinimumHeadSweepInterval ? MinimumHeadSweepInterval : value;
        } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Maximal number of records to peek.
        /// </summary>
        [ObsoleteMetadata(
            Message = "The queue peek batch size has no effect and should be removed from the configuration",
            TreatAsErrorFromVersion = "10",
            RemoveInVersion = "11")]
        [Obsolete("The queue peek batch size has no effect and should be removed from the configuration. Will be treated as an error from version 10.0.0. Will be removed in version 11.0.0.", false)]
        public int? MaxRecordsToPeek
        {
            get;
            set
            {
                if (value.HasValue && value < 1)
                {
                    var message = "Peek batch size is invalid. The value must be greater than zero.";
                    throw new Exception(message);
                }

                if (value.HasValue)
                {
                    Logger.Warn("Configuring the queue peek batch size (MaxRecordsToPeek) has no effect and will be removed in a future version. The number of messages received per peek is bounded by the configured message processing concurrency, and the receive loop stops as soon as the queue is drained.");
                }

                field = value;
            }
        }

        static readonly TimeSpan MinimumHeadSweepInterval = TimeSpan.FromSeconds(1);
        static ILog Logger = LogManager.GetLogger<QueuePeekerOptions>();
    }
}
