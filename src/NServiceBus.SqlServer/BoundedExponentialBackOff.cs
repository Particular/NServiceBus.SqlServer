namespace NServiceBus.Transports.SQLServer
{
    using System;

    /// <summary>
    /// A utility class that does a sleep on very call up to a limit based on a condition.
    /// </summary>
    class BoundedExponentialBackOff : IBackOffStrategy
    {
        readonly int maximum;
        int currentDelay = 50;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="maximum">The maximum number of milliseconds for which the thread is blocked.</param>
        public BoundedExponentialBackOff(int maximum)
        {
            this.maximum = maximum;
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
                currentDelay = RecalculateDelay(currentDelay, maximum);
            }
            else
            {
                currentDelay = 50;
            }
        }

        static int RecalculateDelay(int currentDelay, int maximumDelay)
        {
            var newDelay = currentDelay*2;
            if (newDelay > maximumDelay)
            {
                newDelay = maximumDelay;
            }
            return newDelay;
        }
    }
}
