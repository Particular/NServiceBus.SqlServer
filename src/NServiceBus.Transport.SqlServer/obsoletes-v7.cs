namespace NServiceBus.Transport.SqlServer
{
    using System;

    /// <summary>
    /// Configures native delayed delivery.
    /// </summary>
    public partial class DelayedDeliverySettings
    {
        /// <summary>
        /// Enables the timeout manager for the endpoint.
        /// </summary>
        [ObsoleteEx(
            Message = "Timeout manager has been removed from NServiceBus. See the upgrade guide for more details.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public void EnableTimeoutManagerCompatibility()
        {
            throw new InvalidOperationException();
        }
    }
}