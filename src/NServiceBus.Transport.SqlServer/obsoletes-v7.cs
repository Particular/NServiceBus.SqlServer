#pragma warning disable PS0013 // A Func used as a method parameter with a Task, ValueTask, or ValueTask<T> return type argument should have at least one CancellationToken parameter type argument unless it has a parameter type argument implementing ICancellableContext

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
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#endif
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
            //This is needed due to legacy transport api support. It can be removed when the api is no longer supported.
            if (ConnectionFactory == null && string.IsNullOrWhiteSpace(ConnectionString))
            {
                throw new Exception("SqlServer transport requires connection string or connection factory.");
            }

            if (ConnectionFactory != null && !string.IsNullOrWhiteSpace(ConnectionString))
            {
                throw new Exception(
                    "ConnectionString() and UseCustomConnectionFactory() settings are exclusive and can't be used at the same time.");
            }
        }
    }

    /// <summary>
    /// Provides support for <see cref="UseTransport{T}"/> transport APIs.
    /// </summary>
    public static class SqlServerTransportApiExtensions
    {
        /// <summary>
        /// Configures NServiceBus to use the given transport.
        /// </summary>
        /// <param name="config">Endpoint configuration instance.</param>
        [ObsoleteEx(
            RemoveInVersion = "9",
            TreatAsErrorFromVersion = "8",
            ReplacementTypeOrMember = "EndpointConfiguration.UseTransport(new SqlServerTransport())")]
        public static TransportExtensions<SqlServerTransport> UseTransport<T>(this EndpointConfiguration config)
            where T : SqlServerTransport
        {
            var transport = new SqlServerTransport();

            var routing = config.UseTransport(transport);

            var settings = new TransportExtensions<SqlServerTransport>(transport, routing);

            return settings;
        }
    }

    /// <summary>
    /// Configuration extensions for Message-Driven Pub-Sub compatibility mode
    /// </summary>
    public static partial class MessageDrivenPubSubCompatibilityModeConfiguration
    {
        /// <summary>
        ///    Enables compatibility with endpoints running on message-driven pub-sub
        /// </summary>
        /// <param name="transportExtensions">The transport to enable pub-sub compatibility on</param>
        [ObsoleteEx(Message = "EnableMessageDrivenPubSubCompatibilityMode has been obsoleted.",
            ReplacementTypeOrMember = "RoutingSettings.EnableMessageDrivenPubSubCompatibilityMode",
            RemoveInVersion = "8.0", TreatAsErrorFromVersion = "7.0")]
#pragma warning disable 618
        public static SubscriptionMigrationModeSettings EnableMessageDrivenPubSubCompatibilityMode(
            this TransportExtensions<SqlServerTransport> transportExtensions)
#pragma warning restore 618
        {
            throw new NotImplementedException();
        }
    }
}