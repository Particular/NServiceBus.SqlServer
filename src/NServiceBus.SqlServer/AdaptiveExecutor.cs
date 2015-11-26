namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.CircuitBreakers;

    abstract class AdaptiveExecutor<T> : IExecutor
    {
        protected abstract T Init();
        protected abstract T Try(out bool success);
        protected abstract void Finally(T value);
        protected abstract void HandleException(Exception ex);
        protected abstract IRampUpController CreateRampUpController(Action rampUpCallback);
        protected abstract IBackOffStrategy CreateBackOffStrategy();
        
        protected AdaptiveExecutor(string name, RepeatedFailuresOverTimeCircuitBreaker circuitBreaker, TransportNotifications transportNotifications, TimeSpan slowTaskThreshold)
        {
            this.name = name;
            this.circuitBreaker = circuitBreaker;
            this.transportNotifications = transportNotifications;
            this.slowTaskThreshold = slowTaskThreshold;
        }

        public virtual void Start(int maximumConcurrency, CancellationToken token)
        {
            if (taskTracker != null)
            {
                throw new InvalidOperationException("The executor has already been started.");
            }
            this.token = token;
            taskTracker = new TaskTracker(maximumConcurrency, transportNotifications, name);
            StartTask();
            slowTaskTimer = new Timer(_ => MonitorSlowTasks(), null, TimeSpan.Zero, slowTaskThreshold);
        }

        public virtual void Stop()
        {
            if (taskTracker == null)
            {
                throw new InvalidOperationException("The executor has not been started.");
            }
            using (var waitHandle = new ManualResetEvent(false))
            {
                slowTaskTimer.Dispose(waitHandle);
                waitHandle.WaitOne();
            }
            taskTracker.ShutdownAll();
            taskTracker = null;
        }

        void MonitorSlowTasks()
        {
            var allStates = taskTracker.GetTaskStates();
            foreach (var state in allStates.Cast<ExecutorTaskState>())
            {
                state.TriggerAnotherTaskIfLongRunning(StartTask);
            }
        }

        void StartTask()
        {
            taskTracker.StartAndTrack(() =>
            {
                var state = new ExecutorTaskState();
                var taskId = Guid.NewGuid();
                var receiveTask = Task.Factory
                    .StartNew(ReceiveLoop, state, token, TaskCreationOptions.LongRunning, TaskScheduler.Default)
                    .ContinueWith((t, s) =>
                    {
                        taskTracker.Forget((Guid)s);

                        if (t.IsFaulted)
                        {
                            t.Exception.Handle(ex =>
                            {
                                HandleException(ex);
                                circuitBreaker.Failure(ex);
                                return true;
                            });
                        }

                        if (!taskTracker.ShouldStartAnotherTaskImmediately)
                        {
                            return;
                        }

                        StartTask();
                    }, taskId, token);

                return Tuple.Create(taskId, receiveTask, state);
            });
        }

        void ReceiveLoop(object obj)
        {
            var state = (ExecutorTaskState) obj;
            var backOff = CreateBackOffStrategy();
            var rampUpController = CreateRampUpController(StartTask);

            while (!token.IsCancellationRequested && rampUpController.CheckHasEnoughWork())
            {
                bool success;
                state.Reset();
                rampUpController.RampUpIfTooMuchWork();
                var result = Init();
                try
                {
                    result = Try(out success);
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
                backOff.ConditionalWait(() => !success, Thread.Sleep);
            }
        }

        Timer slowTaskTimer;
        readonly TimeSpan slowTaskThreshold;
        readonly string name;
        readonly RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
        readonly TransportNotifications transportNotifications;
        CancellationToken token;
        TaskTracker taskTracker;
    }
}