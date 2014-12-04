namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    class ReceiveTaskTracker
    {
        readonly object lockObj = new object();
        readonly List<Task> trackedTasks = new List<Task>();
        bool shuttingDown;
        readonly int maximumConcurrency;

        public ReceiveTaskTracker(int maximumConcurrency)
        {
            this.maximumConcurrency = maximumConcurrency;
        }

        public void StartAndTrack(Func<Task> taskFactory)
        {
            lock (lockObj)
            {
                if (!shuttingDown && trackedTasks.Count < maximumConcurrency)
                {
                    var task = taskFactory();
                    trackedTasks.Add(task);
                }
            }
        }

        public void Forget(Task receiveTask)
        {
            lock (lockObj)
            {
                trackedTasks.Remove(receiveTask);
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
    }
}