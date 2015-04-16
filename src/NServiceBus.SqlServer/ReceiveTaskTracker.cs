namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus.Logging;

    class ReceiveTaskTracker : ITaskTracker
    {
        public ReceiveTaskTracker(int maximumConcurrency, TransportNotifications transportNotifications, string queueName)
        {
            this.maximumConcurrency = maximumConcurrency;
            this.transportNotifications = transportNotifications;
            this.queueName = queueName;
        }

        public void StartAndTrack(Func<Tuple<Guid, Task>> taskFactory)
        {
            lock (lockObj)
            {
                if (shuttingDown)
                {
                    Logger.DebugFormat("Ignoring start task request for '{0}' because shutdown is in progress.", queueName);
                    return;
                }
                if (trackedTasks.Count >= maximumConcurrency)
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.DebugFormat("Ignoring start task request for '{0}' because of maximum concurrency limit of {1} has been reached.", queueName, maximumConcurrency);
                    }
                    transportNotifications.InvokeMaximumConcurrencyLevelReached(queueName, maximumConcurrency);
                    return;
                }
                var tuple = taskFactory();
                trackedTasks.Add(tuple.Item1, tuple.Item2);
                if (Logger.IsDebugEnabled)
                {
                    Logger.DebugFormat("Starting a new receive task for '{0}'. Total count current/max {1}/{2}", queueName, trackedTasks.Count, maximumConcurrency);
                }
                transportNotifications.InvokeReceiveTaskStarted(queueName, trackedTasks.Count, maximumConcurrency);
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
                        Logger.DebugFormat("Stopping a receive task for '{0}'. Total count current/max {1}/{2}", queueName, trackedTasks.Count, maximumConcurrency);
                    }
                    transportNotifications.InvokeReceiveTaskStopped(queueName, trackedTasks.Count, maximumConcurrency);
                }
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
                Logger.Debug("Stopping all receive tasks.");
            }
            lock (lockObj)
            {
                shuttingDown = true;
                try
                {
                    Task.WaitAll(trackedTasks.Values.ToArray());
                }
                catch (AggregateException aex)
                {
                    aex.Handle(ex => ex is TaskCanceledException);
                }
            }
        }

        readonly object lockObj = new object();
        readonly Dictionary<Guid, Task> trackedTasks = new Dictionary<Guid, Task>();
        bool shuttingDown;
        readonly int maximumConcurrency;
        readonly TransportNotifications transportNotifications;
        readonly string queueName;

        static readonly ILog Logger = LogManager.GetLogger<ReceiveTaskTracker>();
    }
}