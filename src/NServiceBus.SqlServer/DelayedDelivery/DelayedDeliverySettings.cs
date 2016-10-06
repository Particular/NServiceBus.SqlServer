namespace NServiceBus.Transport.SQLServer
{
    using System;
    using Configuration.AdvanceExtensibility;
    using Settings;

    /// <summary>
    /// Configures native delayed delivery.
    /// </summary>
    public class DelayedDeliverySettings : ExposeSettings
    {
        internal string TableSuffix { get; private set; }
        internal int ProcessingResolution { get; private set; }

        internal DelayedDeliverySettings(SettingsHolder settings) 
            : base(settings)
        {
        }

        /// <summary>
        /// Changes the default suffix of delayed message table (Delayed).
        /// </summary>
        /// <param name="suffix"></param>
        public void MessageStoreTableSuffix(string suffix)
        {
            Guard.AgainstNullAndEmpty(nameof(suffix), suffix);
            TableSuffix = suffix;
        }

        /// <summary>
        /// Changes the default resolution of processing the delayed messages (every 5 seconds).
        /// </summary>
        /// <param name="resolutionInSeconds"></param>
        public void MessageStoreProcessingResolution(int resolutionInSeconds)
        {
            if (resolutionInSeconds <= 0)
            {
                throw new ArgumentException("Processing resolution in seconds has to be a positive number.", nameof(resolutionInSeconds));
            }
            ProcessingResolution = resolutionInSeconds;
        }
    }
}