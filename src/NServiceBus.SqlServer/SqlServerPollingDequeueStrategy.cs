namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using NServiceBus.Features;

    /// <summary>
    ///     A polling implementation of <see cref="IDequeueMessages" />.
    /// </summary>
    class SqlServerPollingDequeueStrategy : IDequeueMessages, IDisposable
    {
        public SqlServerPollingDequeueStrategy(
            LocalConnectionParams localConnectionParams,
            IQueuePurger queuePurger, 
            SecondaryReceiveConfiguration secondaryReceiveConfiguration)
        {
            this.localConnectionParams = localConnectionParams;
            this.queuePurger = queuePurger;
            this.secondaryReceiveConfiguration = secondaryReceiveConfiguration;
        }

        public void Init(DequeueSettings settings)
        {
            queuePurger.Purge(settings.QueueName);

            secondaryReceiveSettings = secondaryReceiveConfiguration.GetSettings(settings.QueueName);

            var primaryQueue = new TableBasedQueue(settings.QueueName, localConnectionParams.Schema);
            availabilitySignallers.Add(new MessageAvailabilitySignaller(primaryQueue, observable, localConnectionParams.PrimaryPollInterval));

            if (secondaryReceiveSettings.IsEnabled)
            {
                var secondaryQueue = new TableBasedQueue(SecondaryReceiveSettings.ReceiveQueue, localConnectionParams.Schema);
                availabilitySignallers.Add(new MessageAvailabilitySignaller(secondaryQueue, observable, localConnectionParams.SecondaryPollInterval));
            }
        }

        public void Start()
        {
            tokenSource = new CancellationTokenSource();
            foreach (var signaller in availabilitySignallers)
            {
                signaller.StartSignalling(tokenSource.Token);
            }
        }

        public IDisposable Subscribe(IObserver<MessageAvailable> observer)
        {
            return observable.Subscribe(observer);
        }

        /// <summary>
        ///     Stops the dequeuing of messages.
        /// </summary>
        public void Stop()
        {
            if (tokenSource == null)
            {
                return;
            }

            tokenSource.Cancel();
        }

        public void Dispose()
        {
            // Injected
        }

        SecondaryReceiveSettings SecondaryReceiveSettings
        {
            get
            {
                if (secondaryReceiveSettings == null)
                {
                    throw new InvalidOperationException("Cannot get secondary receive settings before Init was called.");
                }
                return secondaryReceiveSettings;
            }
        }

        readonly List<MessageAvailabilitySignaller> availabilitySignallers = new List<MessageAvailabilitySignaller>();

        Observable<MessageAvailable> observable = new Observable<MessageAvailable>();
        readonly LocalConnectionParams localConnectionParams;
        readonly IQueuePurger queuePurger;
        readonly SecondaryReceiveConfiguration secondaryReceiveConfiguration;
        SecondaryReceiveSettings secondaryReceiveSettings;
        CancellationTokenSource tokenSource;

    }
}