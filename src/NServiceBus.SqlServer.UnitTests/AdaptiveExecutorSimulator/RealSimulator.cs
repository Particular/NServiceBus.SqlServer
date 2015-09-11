namespace NServiceBus.SqlServer.UnitTests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.CircuitBreakers;
    using NServiceBus.Features;
    using NServiceBus.Transports.SQLServer;
    using NServiceBus.Unicast;
    using NServiceBus.Unicast.Transport;

    class RealSimulator : ISimulator
    {
        readonly string address;
        readonly SqlServerPollingDequeueStrategy dequeueStrategy;
        SqlServerMessageSender sender;
        int messagesProcessed;
        int totalMessages;
        ManualResetEventSlim signalEndEvent = new ManualResetEventSlim(false);
        BlockingCollection<string> results = new BlockingCollection<string>();
        Stopwatch currentTime;
        IDisposable taskStarted;
        IDisposable taskEnded;

        public RealSimulator(string address, string connectionString)
        {
            this.address = address;
            var localConnectionParams = new LocalConnectionParams(null, connectionString, "dbo");
            new SqlServerQueueCreator(new DefaultConnectionStringProvider(localConnectionParams), ConnectionFactory.Default()).CreateQueueIfNecessary(Address.Parse(address), null);

            var transportNotifications = new TransportNotifications();

            taskStarted = transportNotifications.ReceiveTaskStarted.Subscribe(x => AddMessage("Thread started"));
            taskEnded = transportNotifications.ReceiveTaskStopped.Subscribe(x => AddMessage("Thread died"));

            dequeueStrategy = new SqlServerPollingDequeueStrategy(localConnectionParams,
                new ReceiveStrategyFactory(new DummyConnectionStore(), localConnectionParams, Address.Parse("error"), ConnectionFactory.Default()),
                new QueuePurger(new SecondaryReceiveConfiguration(_ => SecondaryReceiveSettings.Disabled()), localConnectionParams, ConnectionFactory.Default()), 
                new SecondaryReceiveConfiguration(_ => SecondaryReceiveSettings.Disabled()),
                transportNotifications,
                new RepeatedFailuresOverTimeCircuitBreaker("A", TimeSpan.FromDays(1000), _ => { }));

            dequeueStrategy.Init(Address.Parse(address), new TransactionSettings(true, TimeSpan.FromMinutes(2), System.Transactions.IsolationLevel.ReadCommitted, 1, false, false),
                ProcessMessage, (message, exception) => { });

            sender = new SqlServerMessageSender(new DefaultConnectionStringProvider(localConnectionParams), new DummyConnectionStore(), new DummyCallbackAddressStore(), ConnectionFactory.Default());
        }

        void AddMessage(string message)
        {
            results.Add(string.Format("{0,12:n} [{1,2}] {2}", currentTime.ElapsedMilliseconds, "", message));
        }

        bool ProcessMessage(TransportMessage m)
        {
            var processingTime = BitConverter.ToInt64(m.Body, 0);
            AddMessage(string.Format("Processing started. Time remaining: {0}", processingTime));
            Thread.Sleep((int)processingTime);
            var processed = Interlocked.Increment(ref messagesProcessed);
            if (processed >= totalMessages)
            {
                AddMessage("Processing finished.");
                signalEndEvent.Set();
            }
            return true;
        }

        public IEnumerable<string> Simulate(Load workLoad, int threadCount)
        {
            currentTime = new Stopwatch();
            currentTime.Start();
            workLoad.Connect(Enqueue);
            totalMessages = workLoad.TotalMessages;
            dequeueStrategy.Start(threadCount);
            
            Task.Run(() =>
            {
                while (workLoad.TotalSentMessages < workLoad.TotalMessages)
                {
                    var before = currentTime.ElapsedMilliseconds;
                    workLoad.TimePassed(before);
                    var after = currentTime.ElapsedMilliseconds;
                    var delay = (int) (after - before);
                    if (delay < 50)
                    {
                        Thread.Sleep(50 - delay);
                    }
                }
                signalEndEvent.Wait();
                taskStarted.Dispose();
                taskEnded.Dispose();
                results.CompleteAdding();
            });
            return results.GetConsumingEnumerable();
        }

        void Enqueue(Message msg)
        {
            sender.Send(new TransportMessage
            {
                Body = BitConverter.GetBytes(msg.ProcessingTime),
                MessageIntent = MessageIntentEnum.Send,
                Recoverable = true,
            }, new SendOptions(address));
        }

        private class DummyCallbackAddressStore : ICallbackAddressStore
        {
            public void SetCallbackAddress(Address callbackAddress)
            {
            }

            public bool TryGetCallbackAddress(out Address callbackAddress)
            {
                callbackAddress = default(Address);
                return false;
            }
        }

        private class DummyConnectionStore : IConnectionStore
        {
            public bool TryGetTransaction(string connectionString, out SqlTransaction transaction)
            {
                transaction = null;
                return false;
            }

            public bool TryGetConnection(string connectionString, out SqlConnection connection)
            {
                connection = null;
                return false;
            }

            public IDisposable SetTransaction(string connectionString, SqlTransaction transaction)
            {
                return new NullDisposable();
            }

            public IDisposable SetConnection(string connectionString, SqlConnection connection)
            {
                return new NullDisposable();
            }

            private class NullDisposable : IDisposable
            {
                public void Dispose()
                {
                }
            }
        }
    }
}