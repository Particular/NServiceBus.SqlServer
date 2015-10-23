using System;
using System.Threading.Tasks;

namespace NServiceBus.Transports.SQLServer
{
    using System.Threading;

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
            this.inputQueue = new TableBasedQueue(settings.InputQueue, "dbo", this.connectionString);
            this.errorQueue = new TableBasedQueue(settings.ErrorQueue, "dbo", this.connectionString);
        }

        public void Start(PushRuntimeSettings limitations)
        {
            Task.Factory.StartNew(() => ProcessMessages(), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        async Task ProcessMessages()
        {
            while (true)
            {
                var strategy = new NoTransactionReceiveStrategy(inputQueue, errorQueue, pipeline);
                await strategy.TryReceiveFrom();
                
            }
        }

        public Task Stop()
        {
            return Task.FromResult(0);
        }
        
    }
}