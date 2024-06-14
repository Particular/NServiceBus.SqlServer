namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;

    class RepeatedFailuresOverTimeCircuitBreaker : IDisposable
    {
        public RepeatedFailuresOverTimeCircuitBreaker(string name, TimeSpan timeToWaitBeforeTriggering, Action<Exception> triggerAction)
        {
            this.name = name;
            this.triggerAction = triggerAction;
            this.timeToWaitBeforeTriggering = timeToWaitBeforeTriggering;

            timer = new Timer(CircuitBreakerTriggered);
        }

        public bool Triggered => triggered;

        public void Dispose()
        {
            //Injected
        }

        public void Success()
        {
            var oldValue = Interlocked.Exchange(ref failureCount, 0);

            if (oldValue == 0)
            {
                return;
            }

            timer.Change(Timeout.Infinite, Timeout.Infinite);
            triggered = false;
            Logger.InfoFormat("The circuit breaker for {0} is now disarmed", name);
        }

        public Task Failure(Exception exception, CancellationToken cancellationToken = default)
        {
            lastException = exception;
            var newValue = Interlocked.Increment(ref failureCount);

            if (newValue == 1)
            {
                timer.Change(timeToWaitBeforeTriggering, NoPeriodicTriggering);
                Logger.WarnFormat("The circuit breaker for {0} is now in the armed state", name);
            }

            var delay = Triggered ? ThrottledDelay : NonThrottledDelay;
            return Task.Delay(delay, cancellationToken);
        }

        void CircuitBreakerTriggered(object state)
        {
            if (Interlocked.Read(ref failureCount) > 0)
            {
                triggered = true;
                Logger.WarnFormat("The circuit breaker for {0} will now be triggered", name);

                try
                {
                    triggerAction(lastException);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error invoking trigger action for circuit breaker {name}", ex);
                }
            }
        }


        string name;
        TimeSpan timeToWaitBeforeTriggering;
        Timer timer;
        Action<Exception> triggerAction;
        long failureCount;
        Exception lastException;
        volatile bool triggered;

        static TimeSpan NoPeriodicTriggering = TimeSpan.FromMilliseconds(-1);
        static ILog Logger = LogManager.GetLogger<RepeatedFailuresOverTimeCircuitBreaker>();
        static TimeSpan NonThrottledDelay = TimeSpan.FromSeconds(1);
        static TimeSpan ThrottledDelay = TimeSpan.FromSeconds(10);
    }
}