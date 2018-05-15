using System;

namespace NServiceBus.SqlServer.TestHarness
{
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    class Program
    {
        public const int MessageCount = 20000;

        static void Main(string[] args)
        {
            Start().GetAwaiter().GetResult();
        }

        static async Task Start()
        {
            var receiverConfig = CreateReceiverConfig();
            receiverConfig.PurgeOnStartup(true);

            //Create queue or purge it if present
            var receiver = await Endpoint.Start(receiverConfig);
            await receiver.Stop();

            var senderConfig = new EndpointConfiguration("TestHarness.Sender");
            senderConfig.SendOnly();
            senderConfig.UsePersistence<InMemoryPersistence>();
            var transport = senderConfig.UseTransport<SqlServerTransport>();
            transport.ConnectionString(@"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True");
            var routing = transport.Routing();
            routing.RouteToEndpoint(typeof(TestMessage), "TestHarness.Receiver");
            routing.RouteToEndpoint(typeof(DoneMessage), "TestHarness.Receiver");
            routing.RouteToEndpoint(typeof(InitMessage), "TestHarness.Receiver");

            var sender = await Endpoint.Start(senderConfig);

            await sender.Send(new InitMessage());

            var concurrencyLimiter = new SemaphoreSlim(8);
            var runningTasks = new ConcurrentDictionary<Task, Task>();
            for (var i = 0; i < MessageCount; i++)
            {
                await concurrencyLimiter.WaitAsync();

                var receiveTask = Send(concurrencyLimiter, sender);
                runningTasks.TryAdd(receiveTask, receiveTask);

#pragma warning disable 4014
                receiveTask.ContinueWith((t, state) =>
#pragma warning restore 4014
                {
                    var receiveTasks = (ConcurrentDictionary<Task, Task>)state;
                    receiveTasks.TryRemove(t, out Task _);
                }, runningTasks, TaskContinuationOptions.ExecuteSynchronously);
            }

            var allTasks = runningTasks.Values.ToArray();
            await Task.WhenAll(allTasks);
            await sender.Send(new DoneMessage());
            await sender.Stop();

            await Task.Delay(2000);

            Console.WriteLine($"Sending {MessageCount} messages complete");

            receiverConfig = CreateReceiverConfig();
            var tcs = new TaskCompletionSource<bool>();
            var stats = new ReceiveStats(tcs);
            receiverConfig.RegisterComponents(c => c.RegisterSingleton(stats));
            receiver = await Endpoint.Start(receiverConfig);

            await tcs.Task;
            await receiver.Stop();

            Console.WriteLine("Press <enter> to exit");
            Console.ReadLine();
        }

        static async Task Send(SemaphoreSlim concurrencyLimiter, IEndpointInstance sender)
        {
            try
            {
                await sender.Send(new TestMessage());

            }
            finally
            {
                concurrencyLimiter.Release();
            }
        }

        static EndpointConfiguration CreateReceiverConfig()
        {
            var receiverConfig = new EndpointConfiguration("TestHarness.Receiver");
            receiverConfig.SendFailedMessagesTo("error");
            receiverConfig.UseTransport<SqlServerTransport>().ConnectionString(@"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True");
            receiverConfig.UsePersistence<InMemoryPersistence>();
            receiverConfig.EnableInstallers();
            return receiverConfig;
        }
    }

    class ReceiveStats
    {
        Stopwatch watch;
        TaskCompletionSource<bool> tcs;
        int testMessagesReceived;

        public ReceiveStats(TaskCompletionSource<bool> tcs)
        {
            this.tcs = tcs;
        }

        public void Test()
        {
            Interlocked.Increment(ref testMessagesReceived);
        }

        public void Init()
        {
            watch = Stopwatch.StartNew();
        }

        public void Done()
        {
            watch.Stop();
            Console.WriteLine($"Received {testMessagesReceived} out of {Program.MessageCount} messages in {watch.ElapsedMilliseconds} ms.");
            tcs.SetResult(true);
        }
    }

    [TimeToBeReceived("00:00:01")]
    class TestMessage : IMessage
    {
    }

    class TestMessageHandler : IHandleMessages<TestMessage>
    {
        ReceiveStats receiveStats;

        public TestMessageHandler(ReceiveStats receiveStats)
        {
            this.receiveStats = receiveStats;
        }

        public Task Handle(TestMessage message, IMessageHandlerContext context)
        {
            receiveStats.Test();
            return Task.CompletedTask;
        }
    }

    class InitMessage : IMessage
    {
    }

    class InitMessageHandler : IHandleMessages<InitMessage>
    {
        ReceiveStats receiveStats;

        public InitMessageHandler(ReceiveStats receiveStats)
        {
            this.receiveStats = receiveStats;
        }

        public Task Handle(InitMessage message, IMessageHandlerContext context)
        {
            receiveStats.Init();
            return Task.CompletedTask;
        }
    }

    class DoneMessage : IMessage
    {
    }

    class DoneMessageHandler : IHandleMessages<DoneMessage>
    {
        ReceiveStats receiveStats;

        public DoneMessageHandler(ReceiveStats receiveStats)
        {
            this.receiveStats = receiveStats;
        }

        public Task Handle(DoneMessage message, IMessageHandlerContext context)
        {
            receiveStats.Done();
            return Task.CompletedTask;
        }
    }
}
