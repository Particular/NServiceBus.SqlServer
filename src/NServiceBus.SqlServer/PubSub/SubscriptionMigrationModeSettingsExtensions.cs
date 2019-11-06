namespace NServiceBus
{
    using System;
    using System.Reflection;
    using Configuration.AdvancedExtensibility;
    using Transport.SQLServer;

    /// <summary>
    /// Extends SubscriptionMigrationModeSettings
    /// </summary>
    public static class SubscriptionMigrationModeSettingsExtensions
    {
        /// <summary>
        /// Registers a given event type as subscribed natively.
        /// </summary>
        public static void SubscribeNatively(this SubscriptionMigrationModeSettings settings, Type eventType)
        {
            settings.GetSettings().GetOrCreate<NativelySubscribedEvents>().Add(t => t == eventType);
        }

        /// <summary>
        /// Registers all event types from a given assembly as subscribed natively.
        /// </summary>
        public static void SubscribeNatively(this SubscriptionMigrationModeSettings settings, Assembly eventAssembly)
        {
            settings.GetSettings().GetOrCreate<NativelySubscribedEvents>().Add(t => t.Assembly == eventAssembly);
        }


        /// <summary>
        /// Registers all event types from a given assembly and namespace as subscribed natively.
        /// </summary>
        public static void SubscribeNatively(this SubscriptionMigrationModeSettings settings, Assembly assembly, string @namespace)
        {
            // empty namespace is null, not string.empty
            @namespace = @namespace == string.Empty ? null : @namespace;

            settings.GetSettings().GetOrCreate<NativelySubscribedEvents>().Add(t => t.Assembly == assembly && t.Namespace == @namespace);
        }
    }
}