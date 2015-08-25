namespace NServiceBus.SqlServer.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus.Transports.SQLServer;

    class Simulator
    {
        readonly int maximumConcurrency;
        readonly Func<Action, IRampUpController> rampUpControllerFactory;
        readonly Load workload;
        long currentTime;
        List<RunningThread> threads = new List<RunningThread>();
        List<Tuple<long, int, string>> result = new List<Tuple<long, int, string>>();
        List<Action> currentQueue = new List<Action>();
        int threadCounter;
        int processedMessages;

        public List<Tuple<long, int, string>> Result
        {
            get { return result; }
        }

        public void DumpResultsToConsole()
        {
            foreach (var tuple in result)
            {
                Console.WriteLine("{0,12:n} [{1,2}] {2}", tuple.Item1, tuple.Item2, tuple.Item3);
            }
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

        public Simulator(int maximumConcurrency, Func<Action, IRampUpController> rampUpControllerFactory, Load workload)
        {
            this.maximumConcurrency = maximumConcurrency;
            this.rampUpControllerFactory = rampUpControllerFactory;
            this.workload = workload;
        }

        public void Simulate()
        {
            StartThread(0);
            while (threads.Count > 0)
            {
                workload.TimePassed(currentTime);
                foreach (var runningThread in threads)
                {
                    runningThread.Tick(currentTime);
                    var blockedTime = currentTime - runningThread.BlockedAt;
                    if (!runningThread.Dead && !runningThread.Replaced && blockedTime > 5000) //blocked for more than five seconds
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
                    if (threads.Any(x => x.LongRunning && !x.Replaced && newTime - currentTime > 5000))
                    {
                        currentTime = currentTime + 5000;
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
        }

        public int StartThread(long dueAt)
        {
            var threadNumber = threadCounter;
            var newThread = new ProcessingThread(rampUpControllerFactory(RampUp), () => workload.TryDequeue(), _ => processedMessages++, s => AddEvent(threadNumber, s));
            var running = new RunningThread(threadNumber, newThread.ReceiveLoop().GetEnumerator(), OnTheadDied, dueAt);
            threads.Add(running);
            threadCounter ++;
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