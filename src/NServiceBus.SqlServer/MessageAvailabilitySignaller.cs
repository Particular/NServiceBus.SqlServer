namespace NServiceBus.Transports.SQLServer
{
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
        readonly BlockingCollection<bool> signalBuffer = new BlockingCollection<bool>(20); 

        public MessageAvailabilitySignaller(TableBasedQueue queue, Observable<MessageAvailable> observable)
        {
            this.queue = queue;
            this.observable = observable;
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
// ReSharper disable once UnusedVariable
            foreach (var signal in signals)
            {
                observable.OnNext(new MessageAvailable(c =>
                {
                    c.Set(queue);
                    c.SetPublicReceiveAddress(queue.QueueName);
                    c.Set<IMessageAvailabilitySignaller>(this);
                }));
            }
        }

        void Poll()
        {
            MessageAvailable();
            Task.Delay(500, stopToken).ContinueWith(task =>
            {
                if (!task.IsCanceled)
                {
                    Poll();
                }
            }, stopToken);
        }
    }
}