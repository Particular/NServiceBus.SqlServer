namespace NServiceBus.SqlServer.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus.Transports.SQLServer;

    class Simulator : ISimulator
    {
        int maximumConcurrency;
        readonly Func<Action, IRampUpController> rampUpControllerFactory;
        long currentTime;
        List<RunningThread> threads = new List<RunningThread>();
        List<Tuple<long, int, string>> result = new List<Tuple<long, int, string>>();
        List<Action> currentQueue = new List<Action>();
        int threadCounter;
        // ReSharper disable once NotAccessedField.Local
        int processedMessages;
        private readonly List<Message> queue = new List<Message>();

        public Message TryDequeue()
        {
            if (queue.Count > 0)
            {
                var result = queue[0];
                queue.RemoveAt(0);
                return result;
            }
            return null;
        }

        void RampUp()
        {
            if (threads.Count < maximumConcurrency)
            {
                var dueAt = currentTime + 1;
                currentQueue.Add(() =>
                {
                    var number = StartThread(dueAt);
                    AddEvent(number, "Thread started");
                });
            }
        }

        void AddEvent(int threadNumber, string eventType)
        {
            result.Add(Tuple.Create(currentTime, threadNumber, eventType));
        }

        public Simulator()
        {
            rampUpControllerFactory = x => new ReceiveRampUpController(x, new TransportNotifications(), "SomeQueue", 7, 5);
        }

        public IEnumerable<string> Simulate(Load workload, int maximumConcurrency)
        {
            this.maximumConcurrency = maximumConcurrency;
            workload.Connect(queue.Add);
            StartThread(0);
            while (threads.Count > 0)
            {
                workload.TimePassed(currentTime);
                const int longRunningThreshold = 100;
                foreach (var runningThread in threads)
                {
                    runningThread.Tick(currentTime);
                    var blockedTime = currentTime - runningThread.BlockedAt;
                    if (!runningThread.Dead && !runningThread.Replaced && runningThread.ProcessingMessage && blockedTime > longRunningThreshold) //blocked for more than five seconds
                    {
                        runningThread.MarkAsReplaced();
                        RampUp();
                    }
                }
                foreach (var action in currentQueue)
                {
                    action();
                }
                currentQueue.Clear();
                if (threads.Count > 0)
                {
                    var newTime = threads.Min(x => x.DueAt);
                    if (threads.Any(x => x.LongRunning && !x.Replaced  && newTime - currentTime > longRunningThreshold))
                    {
                        currentTime = currentTime + longRunningThreshold;
                    }
                    else
                    {
                        currentTime = newTime;
                    }
                }
                //if (!workload.HasMoreMessages && processedMessages == workload.TotalMessages)
                //{
                //    break;
                //}
            }
            return result.Select(x => string.Format("{0,12:n} [{1,2}] {2}", x.Item1, x.Item2, x.Item3));
        }

        int StartThread(long dueAt)
        {
            var threadNumber = threadCounter;
            var newThread = new ProcessingThread(rampUpControllerFactory(RampUp), TryDequeue, _ => processedMessages++, s => AddEvent(threadNumber, s));
            var running = new RunningThread(threadNumber, newThread.ReceiveLoop().GetEnumerator(), OnTheadDied, dueAt);
            threads.Add(running);
            threadCounter++;
            return threadNumber;
        }

        void OnTheadDied(RunningThread obj)
        {
            currentQueue.Add(() =>
            {
                threads.Remove(obj);
                //if (threads.Count == 0)
                //{
                //    StartThread(currentTime + 1);
                //}
                //else
                {
                    AddEvent(obj.ThreadNumber, "Thread died");
                }
            });
        }
    }
}