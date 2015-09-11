namespace NServiceBus.Transports.SQLServer
{
    using System;

    class ReceiveRampUpController : IRampUpController
    {
        int consecutiveSuccesses;
        int consecutiveFailures;
        readonly Action rampUpCallback;
        readonly TransportNotifications transportNotifications;
        readonly string queueName;
        int maximumConsecutiveFailures;
        int minimumConsecutiveSuccesses;

        public ReceiveRampUpController(Action rampUpCallback, TransportNotifications transportNotifications, string queueName, int maximumConsecutiveFailures, int minimumConsecutiveSuccesses)
        {
            this.rampUpCallback = rampUpCallback;
            this.transportNotifications = transportNotifications;
            this.queueName = queueName;
            this.maximumConsecutiveFailures = maximumConsecutiveFailures;
            this.minimumConsecutiveSuccesses = minimumConsecutiveSuccesses;
        }

        public void Succeeded()
        {
            consecutiveSuccesses++;
            consecutiveFailures = 0;
        }

        public void Failed()
        {
            consecutiveFailures++;
            consecutiveSuccesses = 0;
        }

        public bool CheckHasEnoughWork()
        {
            var result = consecutiveFailures < maximumConsecutiveFailures;
            if (!result)
            {
                transportNotifications.InvokeTooLittleWork(queueName);
            }
            return result;
        }

        public void RampUpIfTooMuchWork()
        {
            if (HasTooMuchWork)
            {
                transportNotifications.InvokeTooMuchWork(queueName);
                rampUpCallback();
                consecutiveSuccesses = 0;
                consecutiveFailures = 0;
            }
        }

        private bool HasTooMuchWork
        {
            get { return consecutiveSuccesses > minimumConsecutiveSuccesses; }
        }

    }
}