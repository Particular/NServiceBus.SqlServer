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
        }

        [ObsoleteEx(
            Message = "Pub sub can not be disabled in version 5.0 and above. The transport handles pub-sub natively and does not require a separate subscription persistence.",
            TreatAsErrorFromVersion = "5.0",
            RemoveInVersion = "6.0")]
        public static void DisablePublishing(this TransportExtensions<SqlServerTransport> transportExtensions)
        {
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
        }
    }
}

#pragma warning restore 1591