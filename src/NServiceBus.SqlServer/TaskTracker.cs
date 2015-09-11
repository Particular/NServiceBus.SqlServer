namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus.Logging;

    class TaskTracker
    {
        public TaskTracker(int maximumConcurrency, TransportNotifications transportNotifications, string executorName)
        {
            this.maximumConcurrency = maximumConcurrency;
            this.transportNotifications = transportNotifications;
            this.executorName = executorName;
        }

        public void StartAndTrack(Func<Tuple<Guid, Task, ExecutorTaskState>> taskFactory)
        {
            lock (lockObj)
            {
                if (shuttingDown)
                {
                    Logger.DebugFormat("Ignoring start task request for '{0}' because shutdown is in progress.", executorName);
                    return;
                }
                if (trackedTasks.Count >= maximumConcurrency)
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.DebugFormat("Ignoring start task request for '{0}' because of maximum concurrency limit of {1} has been reached.", executorName, maximumConcurrency);
                    }
                    transportNotifications.InvokeMaximumConcurrencyLevelReached(executorName, maximumConcurrency);
                    return;
                }
                var tuple = taskFactory();
                trackedTasks.Add(tuple.Item1, Tuple.Create(tuple.Item2, tuple.Item3));
                if (Logger.IsDebugEnabled)
                {
                    Logger.DebugFormat("Starting a new receive task for '{0}'. Total count current/max {1}/{2}", executorName, trackedTasks.Count, maximumConcurrency);
                }
                transportNotifications.InvokeReceiveTaskStarted(executorName, trackedTasks.Count, maximumConcurrency);
            }
        }

        public void Forget(Guid id)
        {
            lock (lockObj)
            {
                if (shuttingDown)
                {
                    return;
                }

                if (trackedTasks.Remove(id))
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.DebugFormat("Stopping a receive task for '{0}'. Total count current/max {1}/{2}", executorName, trackedTasks.Count, maximumConcurrency);
                    }
                    transportNotifications.InvokeReceiveTaskStopped(executorName, trackedTasks.Count, maximumConcurrency);
                }
            }
        }

        public IEnumerable<object> GetTaskStates()
        {
            lock (lockObj)
            {
                return trackedTasks.Values.Select(x => x.Item2).ToArray();
            }
        }

        public bool ShouldStartAnotherTaskImmediately
        {
            get
            {
                lock (lockObj)
                {
                    if (shuttingDown)
                    {
                        return false;
                    }
                    return !trackedTasks.Any();
                }
            }
        }

        public void ShutdownAll()
        {
            if (Logger.IsDebugEnabled)
            {
                Logger.Debug("Stopping all tasks.");
            }
            lock (lockObj)
            {
                shuttingDown = true;
                try
                {
                    Task.WaitAll(trackedTasks.Values.Select(x => x.Item1).ToArray());
                }
                catch (AggregateException aex)
                {
                    aex.Handle(ex => ex is TaskCanceledException);
                }
            }
        }

        readonly object lockObj = new object();
        readonly Dictionary<Guid, Tuple<Task, ExecutorTaskState>> trackedTasks = new Dictionary<Guid, Tuple<Task, ExecutorTaskState>>();
        bool shuttingDown;
        readonly int maximumConcurrency;
        readonly TransportNotifications transportNotifications;
        readonly string executorName;

        static readonly ILog Logger = LogManager.GetLogger<TaskTracker>();
    }
}