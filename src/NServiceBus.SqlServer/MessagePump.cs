using System;
using System.Threading.Tasks;

namespace NServiceBus.Transports.SQLServer
{
    using System.Threading;
    using System.Transactions;

    class MessagePump : IPushMessages
    {
        TableBasedQueue inputQueue;
        TableBasedQueue errorQueue;
        Func<PushContext, Task> pipeline;
        string connectionString;

        public MessagePump(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public void Init(Func<PushContext, Task> pipe, PushSettings settings)
        {
            this.pipeline = pipe;
            this.inputQueue = new TableBasedQueue(settings.InputQueue, "dbo");
            this.errorQueue = new TableBasedQueue(settings.ErrorQueue, "dbo");
        }

        public void Start(PushRuntimeSettings limitations)
        {
            Task.Factory.StartNew(() => ProcessMessages(), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        async Task ProcessMessages()
        {
            while (true)
            {
                //var transactionOptions = new TransactionOptions
                //{
                //    IsolationLevel =  IsolationLevel.Serializable,
                //    Timeout = TimeSpan.FromSeconds(10)
                //};
                var strategy = new ReceiveWithNoTransaction(this.connectionString);
                await strategy.TryReceiveFrom(inputQueue, errorQueue, pipeline);
            }
        }

        public Task Stop()
        {
            return Task.FromResult(0);
        }
        
    }
}