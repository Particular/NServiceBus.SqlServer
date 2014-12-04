namespace NServiceBus.Transports.SQLServer
{
    using System;

    class RampUpController
    {
        int consecutiveSuccesses;
        int consecutiveFailures;
        readonly Action rampUpCallback;

        public RampUpController(Action rampUpCallback)
        {
            this.rampUpCallback = rampUpCallback;
        }

        public void ReadSucceeded()
        {
            consecutiveSuccesses++;
            consecutiveFailures = 0;
        }

        public void ReadFailed()
        {
            consecutiveFailures++;
            consecutiveSuccesses = 0;
        }

        public bool HasEnoughWork
        {
            get { return consecutiveFailures < 3; }
        }

        public void RampUpIfTooMuchWork()
        {
            if (HasTooMuchWork)
            {
                rampUpCallback();
                consecutiveSuccesses = 0;
                consecutiveFailures = 0;
            }
        }

        private bool HasTooMuchWork
        {
            get { return consecutiveSuccesses > 5; }
        }

    }
}