#pragma warning disable 1591

namespace NServiceBus
{
    using System;
    using System.Reflection;
    using Pipeline;

    public static class MessageDrivenPubSubCompatibility
    {
        [ObsoleteEx(
            Message = @"Subscription authorization has been moved to message-driven pub-sub migration mode. 

var compatMode = transport.EnableMessageDrivenPubSubCompatibilityMode();
compatMode.SubscriptionAuthorizer(authorizer);",
            ReplacementTypeOrMember = "SubscriptionMigrationModeSettings.SubscriptionAuthorizer(transportExtensions, authorizer)",
            TreatAsErrorFromVersion = "5.0",
            RemoveInVersion = "6.0")]
        public static void SubscriptionAuthorizer(this TransportExtensions<SqlServerTransport> transportExtensions, Func<IIncomingPhysicalMessageContext, bool> authorizer)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            Message = "Publishing can not be disabled in version 5.0 and above. The transport handles publish-subscribe natively and does not require a separate subscription persistence.",
            TreatAsErrorFromVersion = "5.0",
            RemoveInVersion = "6.0")]
        public static void DisablePublishing(this TransportExtensions<SqlServerTransport> transportExtensions)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            Message = @"Publisher registration has been moved to message-driven pub-sub migration mode.

var compatMode = transport.EnableMessageDrivenPubSubCompatibilityMode();
compatMode.RegisterPublisher(eventType, publisherEndpoint);",
            ReplacementTypeOrMember = "SubscriptionMigrationModeSettings.RegisterPublisher(routingSettings, eventType, publisherEndpoint)",
            TreatAsErrorFromVersion = "5.0",
            RemoveInVersion = "6.0")]
        public static void RegisterPublisher(this RoutingSettings<SqlServerTransport> routingSettings, Type eventType, string publisherEndpoint)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            Message = @"Publisher registration has been moved to message-driven pub-sub migration mode.

var compatMode = transport.EnableMessageDrivenPubSubCompatibilityMode();
compatMode.RegisterPublisher(assembly, publisherEndpoint);",
            ReplacementTypeOrMember = "SubscriptionMigrationModeSettings.RegisterPublisher(routingSettings, assembly, publisherEndpoint)",
            TreatAsErrorFromVersion = "5.0",
            RemoveInVersion = "6.0")]
        public static void RegisterPublisher(this RoutingSettings<SqlServerTransport> routingSettings, Assembly assembly, string publisherEndpoint)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            Message = @"Publisher registration has been moved to message-driven pub-sub migration mode.

var compatMode = transport.EnableMessageDrivenPubSubCompatibilityMode();
compatMode.RegisterPublisher(assembly, namespace, publisherEndpoint);",
            ReplacementTypeOrMember = "SubscriptionMigrationModeSettings.RegisterPublisher(routingSettings, assembly, namespace, publisherEndpoint)",
            TreatAsErrorFromVersion = "5.0",
            RemoveInVersion = "6.0")]
        public static void RegisterPublisher(this RoutingSettings<SqlServerTransport> routingSettings, Assembly assembly, string @namespace, string publisherEndpoint)
        {
            throw new NotImplementedException();
        }
    }
}

namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Data.SqlClient;

    public static partial class SendOptionsExtensions
    {
        [ObsoleteEx(
                RemoveInVersion = "6.0",
                TreatAsErrorFromVersion = "5.0",
                ReplacementTypeOrMember = "UseCustomSqlTransaction",
                Message = "The connection parameter is no longer required.")]
        public static void UseCustomSqlConnectionAndTransaction(this SendOptions options, SqlConnection connection, SqlTransaction transaction)
        {
            throw new NotImplementedException();
        }
    }
}

namespace NServiceBus.Transport.SQLServer
{
    using System;

    public static partial class SqlServerTransportSettingsExtensions
    {
        [ObsoleteEx(
            RemoveInVersion = "6.0",
            TreatAsErrorFromVersion = "5.0",
            ReplacementTypeOrMember = "NativeDelayedDelivery",
            Message = "Starting from version 5 native delayed delivery is always enabled. It can be configured via NativeDelayedDelivery")]
        public static DelayedDeliverySettings UseNativeDelayedDelivery(this TransportExtensions<SqlServerTransport> transportExtensions)
        {
            throw new NotImplementedException();
        }
    }
}



#pragma warning restore 1591