namespace NServiceBus.Transport.SQLServer
{
    using Features;

    class CompatibilityModeSubscriptionValidationFeature : Feature
    {
        public CompatibilityModeSubscriptionValidationFeature()
        {
            EnableByDefault();
            Defaults(s => s.SetDefault(new NativelySubscribedEvents()));
            Prerequisite(c => c.Settings.HasSetting("SqlServer.Subscriptions.EnableMigrationMode"), "SQL Server transport runs in message-driven pub/sub compatibility mode");
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Pipeline.Register(new CompatibilityModeSubscriptionValidationBehavior(context.Settings.Get<NativelySubscribedEvents>()),
                "Validates correct subscription configuration in the message-driven pub/sub compatibility mode");
        }
    }
}