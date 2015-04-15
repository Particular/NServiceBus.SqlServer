namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.CircuitBreakers;

    abstract class AdaptiveExecutor<T> : IExecutor
    {
        protected abstract T Init();
        protected abstract T Try(T initialValue, out bool success);
        protected abstract void Finally(T value);
        protected abstract void HandleException(Exception ex);
        protected abstract IRampUpController CreateRampUpController(Action rampUpCallback);
        protected abstract ITaskTracker CreateTaskTracker(int maximumConcurrency);

        protected AdaptiveExecutor(RepeatedFailuresOverTimeCircuitBreaker circuitBreaker)
        {
            this.circuitBreaker = circuitBreaker;
        }

        public virtual void Start(int maximumConcurrency, CancellationTokenSource tokenSource)
        {
            if (taskTracker != null)
            {
                throw new InvalidOperationException("The executor has already been started. Use Stop() to stop it.");
            }
            this.tokenSource = tokenSource;
            taskTracker = CreateTaskTracker(maximumConcurrency);
            StartTask();
        }

        public virtual void Stop()
        {
            taskTracker.ShutdownAll();
            taskTracker = null;
            tokenSource = null;
        }

        void StartTask()
        {
            var token = tokenSource.Token;

            taskTracker.StartAndTrack(() =>
            {
                var receiveTask = Task.Factory
                    .StartNew(ReceiveLoop, null, token, TaskCreationOptions.LongRunning, TaskScheduler.Default)
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            t.Exception.Handle(ex =>
                            {
                                HandleException(ex);
                                circuitBreaker.Failure(ex);
                                return true;
                            });
                        }
                        taskTracker.Forget(t);
                        if (taskTracker.HasNoTasks)
                        {
                            StartTask();
                        }
                    });

                return receiveTask;
            });
        }

        void ReceiveLoop(object obj)
        {
            var backOff = new BackOff(1000);
            var rampUpController = CreateRampUpController(StartTask);

            while (!tokenSource.IsCancellationRequested && rampUpController.CheckHasEnoughWork())
            {
                bool success;
                rampUpController.RampUpIfTooMuchWork();
                var result = Init();
                try
                {
                    result = Try(result, out success);
                    if (success)
                    {
                        rampUpController.Succeeded();
                    }
                    else
                    {
                        rampUpController.Failed();
                    }
                }
                finally
                {
                    Finally(result);
                }

                circuitBreaker.Success();
                backOff.Wait(() => !success);
            }
        }

        readonly RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
        CancellationTokenSource tokenSource;
        ITaskTracker taskTracker;

    }
}