namespace NServiceBus.Transports.SQLServer
{
    using System;

    /// <summary>
    /// Pipeline notifications
    /// </summary>
    public class TransportNotifications : IDisposable
    {
        /// <summary>
        /// Notifies of starting a new receive task in the transport receiver.
        /// </summary>
        public IObservable<ReceiveTaskStarted> ReceiveTaskStarted
        {
            get { return receiveTaskStarted; }
        }

        /// <summary>
        /// Notifies of stopping a receive task in the transport receiver.
        /// </summary>
        public IObservable<ReceiveTaskStopped> ReceiveTaskStopped
        {
            get { return receiveTaskStopped; }
        }

        /// <summary>
        /// Notifies of reaching maxium concurrency level and hence not starting a new receive task
        /// </summary>
        public IObservable<MaximumConcurrencyLevelReached> MaximumConcurrencyLevelReached
        {
            get { return maximumConcurrencyLevelReached; }
        }

        /// <summary>
        /// Notifies of detecting too much work for current number of receive tasks
        /// </summary>
        public IObservable<TooMuchWork> TooMuchWork
        {
            get { return tooMuchWork; }
        }

        /// <summary>
        /// Notifies of detecting too little work for current number of receive tasks
        /// </summary>
        public IObservable<TooLittleWork> TooLittleWork
        {
            get { return tooLittleWork; }
        }

        void IDisposable.Dispose()
        {
            //Injected
        }

        internal void InvokeReceiveTaskStarted(string queue, int currencyConcurrency, int maximumConcurrency)
        {
            receiveTaskStarted.OnNext(new ReceiveTaskStarted(queue, currencyConcurrency, maximumConcurrency));
        }
        
        internal void InvokeReceiveTaskStopped(string queue, int currencyConcurrency, int maximumConcurrency)
        {
            receiveTaskStopped.OnNext(new ReceiveTaskStopped(queue, currencyConcurrency, maximumConcurrency));
        }

        internal void InvokeMaximumConcurrencyLevelReached(string queue,int maximumConcurrency)
        {
            maximumConcurrencyLevelReached.OnNext(new MaximumConcurrencyLevelReached(queue, maximumConcurrency));
        }

        internal void InvokeTooMuchWork(string queue)
        {
            tooMuchWork.OnNext(new TooMuchWork(queue));
        }

        internal void InvokeTooLittleWork(string queue)
        {
            tooLittleWork.OnNext(new TooLittleWork(queue));
        }

        Observable<ReceiveTaskStarted> receiveTaskStarted = new Observable<ReceiveTaskStarted>();
        Observable<ReceiveTaskStopped> receiveTaskStopped = new Observable<ReceiveTaskStopped>();
        Observable<MaximumConcurrencyLevelReached> maximumConcurrencyLevelReached = new Observable<MaximumConcurrencyLevelReached>();
        Observable<TooMuchWork> tooMuchWork = new Observable<TooMuchWork>();
        Observable<TooLittleWork> tooLittleWork = new Observable<TooLittleWork>();
    }

    /// <summary>
    /// Notifies of starting a new receive task in the transport receiver.
    /// </summary>
    public struct ReceiveTaskStarted
    {
        /// <summary>
        /// Maximum concurrency level of transport receiver
        /// </summary>
        public readonly int MaximumConcurrency;

        /// <summary>
        /// Current count of running receive tasks
        /// </summary>
        public readonly int CurrentConcurrency;

        /// <summary>
        /// Name of the source queue
        /// </summary>
        public readonly string Queue;


        /// <summary>
        /// Creates new instance of <see cref="ReceiveTaskStarted"/>
        /// </summary>
        /// <param name="queue"></param>
        /// <param name="currentConcurrency"></param>
        /// <param name="maximumConcurrency"></param>
        public ReceiveTaskStarted(string queue, int currentConcurrency, int maximumConcurrency)
        {
            Queue = queue;
            MaximumConcurrency = maximumConcurrency;
            CurrentConcurrency = currentConcurrency;
        }
    }

    /// <summary>
    /// Notifies of stopping a receive task in the transport receiver.
    /// </summary>
    public struct ReceiveTaskStopped
    {
        /// <summary>
        /// Maximum concurrency level of transport receiver
        /// </summary>
        public readonly int MaximumConcurrency;
        /// <summary>
        /// Current count of running receive tasks
        /// </summary>
        public readonly int CurrentConcurrency;

        /// <summary>
        /// Name of the source queue
        /// </summary>
        public readonly string Queue;

        /// <summary>
        /// Creates new instance of <see cref="ReceiveTaskStopped"/>
        /// </summary>
        /// <param name="queue"></param>
        /// <param name="currentConcurrency"></param>
        /// <param name="maximumConcurrency"></param>
        public ReceiveTaskStopped(string queue, int currentConcurrency, int maximumConcurrency)
        {
            Queue = queue;
            MaximumConcurrency = maximumConcurrency;
            CurrentConcurrency = currentConcurrency;
        }
    }

    /// <summary>
    /// Notifies of reaching maxium concurrency level and hence not starting a new receive task
    /// </summary>
    public struct MaximumConcurrencyLevelReached
    {
        /// <summary>
        /// Maximum concurrency level of transport receiver
        /// </summary>
        public readonly int MaximumConcurrency;

        /// <summary>
        /// Name of the source queue
        /// </summary>
        public readonly string Queue;

        /// <summary>
        /// Creates new instance of <see cref="MaximumConcurrencyLevelReached"/>
        /// </summary>
        /// <param name="queue"></param>
        /// <param name="maximumConcurrency"></param>
        public MaximumConcurrencyLevelReached(string queue, int maximumConcurrency)
        {
            Queue = queue;
            MaximumConcurrency = maximumConcurrency;
        }
    }

    /// <summary>
    /// Notifies of detecting too much work for current number of receive tasks
    /// </summary>
    public struct TooMuchWork
    {
        /// <summary>
        /// Name of the source queue
        /// </summary>
        public readonly string Queue;

        /// <summary>
        /// Creates new instance of <see cref="TooMuchWork"/>
        /// </summary>
        /// <param name="queue"></param>
        public TooMuchWork(string queue)
        {
            Queue = queue;
        }
    }

    /// <summary>
    /// Notifies of detecting too little work for current number of receive tasks
    /// </summary>
    public struct TooLittleWork
    {
        /// <summary>
        /// Name of the source queue
        /// </summary>
        public readonly string Queue;

        /// <summary>
        /// Creates new instance of <see cref="TooLittleWork"/>
        /// </summary>
        /// <param name="queue"></param>
        public TooLittleWork(string queue)
        {
            Queue = queue;
        }
    }
}