namespace NServiceBus.Transports.SQLServer
{
    using System;

    /// <summary>
    /// A utility class that does a sleep on very call up to a limit based on a condition.
    /// </summary>
    class BoundedExponentialBackOff : IBackOffStrategy
    {
        static readonly int defaultDelay = 50;
        readonly int maximumDelay;
        int currentDelay = defaultDelay;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="maximumDelay">The maximum number of milliseconds for which the thread is blocked.</param>
        public BoundedExponentialBackOff(int maximumDelay)
        {
            this.maximumDelay = maximumDelay;
        }

        /// <summary>
        /// It executes the Thread sleep if condition is <c>true</c>, otherwise it resets.
        /// </summary>
        /// <param name="condition">If the condition is <c>true</c> then the wait is performed.</param>
        /// <param name="waitAction"></param>
        public void ConditionalWait(Func<bool> condition, Action<int> waitAction)
        {
            if (condition())
            {
                waitAction(currentDelay);
                currentDelay = RecalculateDelay(currentDelay, maximumDelay);
            }
            else
            {
                currentDelay = defaultDelay;
            }
        }

        static int RecalculateDelay(int currentDelay, int maximumDelay)
        {
            return Math.Min(maximumDelay, currentDelay * 2);
        }
    }
}
