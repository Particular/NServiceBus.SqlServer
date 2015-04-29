namespace NServiceBus.Transports.SQLServer
{
    using System;

    class SecondaryReceiveSettings
    {
        public static SecondaryReceiveSettings Disabled()
        {
            return new SecondaryReceiveSettings();
        }

        public static SecondaryReceiveSettings Enabled(string secondaryReceiveQueue)
        {
            if (string.IsNullOrEmpty(secondaryReceiveQueue))
            {
                throw new ArgumentException("Receive queue must not be empty.","secondaryReceiveQueue");
            }
            return new SecondaryReceiveSettings()
            {
                ReceiveQueue = secondaryReceiveQueue,
                IsEnabled = true
            };
        }

        public bool IsEnabled { get; private set; }
        public string ReceiveQueue { get; private set; }

        private SecondaryReceiveSettings()
        {
        }
    }
}