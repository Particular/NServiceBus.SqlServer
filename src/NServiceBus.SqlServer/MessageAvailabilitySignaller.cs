namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;

    interface IMessageAvailabilitySignaller
    {
        void MessageAvailable();
    }

    class MessageAvailabilitySignaller : IMessageAvailabilitySignaller
    {
        CancellationToken stopToken;
        readonly TableBasedQueue queue;
        readonly Observable<MessageAvailable> observable;
        readonly BlockingCollection<bool> signalBuffer = new BlockingCollection<bool>(5);
        readonly int pollInterval;

        public MessageAvailabilitySignaller(TableBasedQueue queue, Observable<MessageAvailable> observable, int pollInterval)
        {
            this.queue = queue;
            this.observable = observable;
            this.pollInterval = pollInterval;
        }

        public void StartSignalling(CancellationToken stopToken)
        {
            this.stopToken = stopToken;
            Task.Factory.StartNew(Loop, stopToken);
            Task.Factory.StartNew(Poll, stopToken);
        }

        public void MessageAvailable()
        {
            signalBuffer.TryAdd(true);
        }

        void Loop()
        {
            var signals = signalBuffer.GetConsumingEnumerable(stopToken);
            try
            {
                // ReSharper disable once UnusedVariable
                foreach (var signal in signals)
                {
                    observable.OnNext(new MessageAvailable(queue.QueueName, c =>
                    {
                        c.Set(queue);
                        c.Set<IMessageAvailabilitySignaller>(this);
                    }));
                }
            }
            catch (OperationCanceledException)
            {
                //Shutting down
            }

        }

        void Poll()
        {
            MessageAvailable();
            Task.Delay(pollInterval, stopToken).ContinueWith(task =>
            {
                if (!task.IsCanceled)
                {
                    Poll();
                }
            }, stopToken);
        }
    }
}