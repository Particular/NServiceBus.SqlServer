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

        public void StartAndTrack(Func<Task> taskFactory)
        {
            lock (lockObj)
            {
                if (shuttingDown)
                {
                    Logger.Debug("Ignoring start task request because shutdown is in progress.");
                    return;
                }
                if (trackedTasks.Count >= maximumConcurrency)
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.DebugFormat("Ignoring start task request because of maximum concurrency limit {0}", maximumConcurrency);
                    }
                    transportNotifications.InvokeMaximumConcurrencyLevelReached(queueName, maximumConcurrency);
                    return;
                }
                var task = taskFactory();
                trackedTasks.Add(task);
                if (Logger.IsDebugEnabled)
                {
                    Logger.DebugFormat("Starting a new receive task. Total count current/max {0}/{1}", trackedTasks.Count, maximumConcurrency);
                }
                transportNotifications.InvokeReceiveTaskStarted(queueName, trackedTasks.Count, maximumConcurrency);
            }
        }

        public void Forget(Task receiveTask)
        {
            lock (lockObj)
            {
                trackedTasks.Remove(receiveTask);
                if (Logger.IsDebugEnabled)
                {
                    Logger.DebugFormat("Stopping a receive task. Total count current/max {0}/{1}", trackedTasks.Count, maximumConcurrency);
                }
                transportNotifications.InvokeReceiveTaskStopped(queueName, trackedTasks.Count, maximumConcurrency);
            }
        }

        public bool HasNoTasks
        {
            get
            {
                lock (lockObj)
                {
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
            Task[] awaitedTasks;
            lock (lockObj)
            {
                shuttingDown = true;
                awaitedTasks = trackedTasks.ToArray();
            }

            try
            {
                Task.WaitAll(awaitedTasks);
            }
            catch (AggregateException aex)
            {
                aex.Handle(ex => ex is TaskCanceledException);
            }
        }

        readonly object lockObj = new object();
        readonly List<Task> trackedTasks = new List<Task>();
        bool shuttingDown;
        readonly int maximumConcurrency;
        readonly TransportNotifications transportNotifications;
        readonly string queueName;

        static readonly ILog Logger = LogManager.GetLogger<ReceiveTaskTracker>();
    }
}