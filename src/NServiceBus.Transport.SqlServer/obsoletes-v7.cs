namespace NServiceBus.Transport.SqlServer
{
    using System;

    public partial class DelayedDeliverySettings
    {
        /// <summary>
        /// Enables the timeout manager for the endpoint.
        /// </summary>
        [ObsoleteEx(
            Message = "Timeout manager has been removed from NServiceBus. See the upgrade guide for more details.",
            RemoveInVersion = "8.0", TreatAsErrorFromVersion = "7.0")]
        public void EnableTimeoutManagerCompatibility() => throw new InvalidOperationException();

        /// <summary>
        /// Configures how often delayed messages are processed.
        /// </summary>
        [ObsoleteEx(
            Message =
                "Delayed message polling now uses an adaptive delays and no longer needs a processing interval. This setting is safe to remove.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public void ProcessingInterval(TimeSpan interval) => throw new NotImplementedException();
    }

    public partial class DelayedDeliveryOptions
    {
        /// <summary>
        /// Configures how often delayed messages are processed.
        /// </summary>
        [ObsoleteEx(
            Message =
                "Delayed message polling now uses an adaptive delays and no longer needs a processing interval. This setting is safe to remove.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public TimeSpan ProcessingInterval
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }
    }
}

namespace NServiceBus
{
    using System;

    /// <summary>
    /// SqlServer Transport
    /// </summary>
    public partial class SqlServerTransport
    {
        /// <summary>
        /// Used for backwards compatibility with the legacy transport api.
        /// </summary>
        internal SqlServerTransport()
            : base(TransportTransactionMode.TransactionScope, true, true, true)
        {
        }

        void ValidateConfiguration()
        {
        }
    }
}